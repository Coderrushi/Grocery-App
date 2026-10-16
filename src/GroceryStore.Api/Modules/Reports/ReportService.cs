using GroceryStore.Api.Common;
using GroceryStore.Api.Common.Errors;
using GroceryStore.Api.Data;
using GroceryStore.Api.Modules.Catalog;
using GroceryStore.Api.Modules.Identity;
using GroceryStore.Api.Modules.Orders;
using GroceryStore.Api.Modules.Wallet;
using Microsoft.EntityFrameworkCore;

namespace GroceryStore.Api.Modules.Reports;

/// <summary>
/// Read-only figures for the shopkeeper, computed live from the order, stock and wallet ledgers with
/// GROUP BY queries (indexes: orders(Status, PlacedAtUtc), inventory_adjustments(ProductId, CreatedAtUtc)).
/// For one shop this stays fast for years; if it ever doesn't, daily summary tables can be added behind
/// the same methods.
/// "Sales" = orders placed in the period, minus cancelled ones, counted on their IST order day.
/// </summary>
public sealed class ReportService(AppDbContext db, AdminOrderService adminOrders, TimeProvider clock)
{
    private const int TopCount = 10;
    private const int SalesVelocityDays = 30;

    private static readonly OrderStatus[] OpenStatuses =
        [OrderStatus.Placed, OrderStatus.Confirmed, OrderStatus.Packed, OrderStatus.OutForDelivery];

    private IQueryable<Order> Orders => db.Set<Order>();
    private IQueryable<Order> Sold => Orders.Where(o => o.Status != OrderStatus.Cancelled);

    public ResolvedRange Resolve(ReportRange range)
    {
        var to = range.To ?? ShopTime.Today(clock);
        var from = range.From ?? to.AddDays(-(ReportRange.DefaultDays - 1));
        if (to < from || to.DayNumber - from.DayNumber >= ReportRange.MaxDays)
        {
            throw new BusinessRuleException($"Choose a period of 1 to {ReportRange.MaxDays} days, ending on or after its start.");
        }

        return new ResolvedRange(from, to);
    }

    // ---------- Dashboard ----------

    public async Task<DashboardResponse> GetDashboardAsync(CancellationToken ct)
    {
        var today = ShopTime.Today(clock);
        var week = await DailySalesAsync(new ResolvedRange(today.AddDays(-6), today), ct);
        var todayRow = week[^1];
        var yesterdayRow = week[^2];

        var open = await Orders.Where(o => OpenStatuses.Contains(o.Status))
            .GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var activeProducts = db.Set<Product>().Where(p => p.IsActive);
        var outOfStock = await activeProducts.CountAsync(p => p.StockQuantity == 0, ct);
        var lowStock = await activeProducts.CountAsync(p => p.StockQuantity > 0 && p.StockQuantity <= p.LowStockThreshold, ct);
        var alerts = await activeProducts.Where(p => p.StockQuantity <= p.LowStockThreshold)
            .OrderBy(p => p.StockQuantity).ThenBy(p => p.Name)
            .Take(8)
            .Select(p => new StockAlert(p.Id, p.Name, p.Brand, p.UnitLabel, p.StockQuantity, p.LowStockThreshold))
            .ToListAsync(ct);

        return new DashboardResponse(
            today,
            Figures(todayRow),
            Figures(yesterdayRow),
            todayRow.CancelledOrders,
            [.. OpenStatuses.Select(s => new StatusCount(s, open.FirstOrDefault(x => x.Status == s)?.Count ?? 0))],
            week,
            outOfStock,
            lowStock,
            alerts,
            await adminOrders.LatestAsync(5, ct));
    }

    private static SalesFigures Figures(DailySales day) => new(day.Orders, day.Sales, Average(day.Sales, day.Orders));

    private static decimal Average(decimal total, int count) => count == 0 ? 0m : Math.Round(total / count, 2);

    // ---------- Sales ----------

    public async Task<SalesReportResponse> GetSalesAsync(ResolvedRange range, CancellationToken ct)
    {
        var rows = await DayStatusRowsAsync(range, ct);
        var sold = rows.Where(r => r.Status != OrderStatus.Cancelled).ToList();
        var cancelled = rows.Where(r => r.Status == OrderStatus.Cancelled).ToList();
        var orders = sold.Sum(r => r.Orders);
        var sales = sold.Sum(r => r.Total);
        var delivered = sold.Where(r => r.Status == OrderStatus.Delivered).Sum(r => r.Total);

        var totals = new SalesTotals(
            orders,
            sales,
            ItemSales: sold.Sum(r => r.Subtotal),
            DeliveryFees: sold.Sum(r => r.DeliveryFee),
            CustomerSavings: sold.Sum(r => r.MrpTotal - r.Subtotal),
            AverageOrderValue: Average(sales, orders),
            DeliveredSales: delivered,
            InProgressSales: sales - delivered,
            CancelledOrders: cancelled.Sum(r => r.Orders),
            CancelledValue: cancelled.Sum(r => r.Total));

        var products = await ProductSalesAsync(range, ct);
        return new SalesReportResponse(
            range.From,
            range.To,
            totals,
            ToDays(range, rows),
            [.. products.OrderByDescending(p => p.Sales).ThenBy(p => p.Name).Take(TopCount)],
            [.. products.OrderByDescending(p => p.Quantity).ThenBy(p => p.Name).Take(TopCount)],
            await CategorySalesAsync(range, ct));
    }

    public async Task<IReadOnlyList<DailySales>> DailySalesAsync(ResolvedRange range, CancellationToken ct) =>
        ToDays(range, await DayStatusRowsAsync(range, ct));

    private sealed record DayStatusRow(
        DateOnly Day, OrderStatus Status, int Orders, decimal Total, decimal Subtotal, decimal DeliveryFee, decimal MrpTotal);

    /// <summary>One row per (IST day, status): the single query behind the daily chart and the totals.</summary>
    private async Task<List<DayStatusRow>> DayStatusRowsAsync(ResolvedRange range, CancellationToken ct)
    {
        var rows = await Orders
            .Where(o => o.PlacedAtUtc >= range.FromUtc && o.PlacedAtUtc < range.ToUtcExclusive)
            // Shift to IST before taking the date: translates to DATE(DATE_ADD(PlacedAtUtc, INTERVAL 330 MINUTE)).
            .GroupBy(o => new { Day = o.PlacedAtUtc.AddMinutes(ShopTime.OffsetMinutes).Date, o.Status })
            .Select(g => new
            {
                g.Key.Day,
                g.Key.Status,
                Orders = g.Count(),
                Total = g.Sum(o => o.Total),
                Subtotal = g.Sum(o => o.Subtotal),
                DeliveryFee = g.Sum(o => o.DeliveryFee),
                MrpTotal = g.Sum(o => o.MrpTotal),
            })
            .ToListAsync(ct);

        return [.. rows.Select(r => new DayStatusRow(
            DateOnly.FromDateTime(r.Day), r.Status, r.Orders, r.Total, r.Subtotal, r.DeliveryFee, r.MrpTotal))];
    }

    /// <summary>Every day of the period, including days without orders (so charts have no gaps).</summary>
    private static List<DailySales> ToDays(ResolvedRange range, List<DayStatusRow> rows)
    {
        var byDay = rows.ToLookup(r => r.Day);
        return [.. range.Days.Select(day =>
        {
            var sold = byDay[day].Where(r => r.Status != OrderStatus.Cancelled).ToList();
            return new DailySales(
                day,
                sold.Sum(r => r.Orders),
                sold.Sum(r => r.Total),
                byDay[day].Where(r => r.Status == OrderStatus.Cancelled).Sum(r => r.Orders));
        })];
    }

    private IQueryable<OrderItem> SoldItems(ResolvedRange range) =>
        from item in db.Set<OrderItem>()
        join order in Sold on item.OrderId equals order.Id
        where order.PlacedAtUtc >= range.FromUtc && order.PlacedAtUtc < range.ToUtcExclusive
        select item;

    /// <summary>Every product sold in the period (by item value, delivery fees excluded), with today's name.</summary>
    public async Task<List<ProductSales>> ProductSalesAsync(ResolvedRange range, CancellationToken ct)
    {
        var totals = SoldItems(range)
            .GroupBy(i => i.ProductId)
            .Select(g => new { ProductId = g.Key, Quantity = g.Sum(i => i.Quantity), Sales = g.Sum(i => i.LineTotal) });

        return await (
            from t in totals
            join p in db.Set<Product>() on t.ProductId equals p.Id
            orderby t.Sales descending, p.Name
            select new ProductSales(p.Id, p.Name, p.Brand, p.UnitLabel, t.Quantity, t.Sales))
            .ToListAsync(ct);
    }

    /// <summary>By each product's current category (moving a product moves its history with it).</summary>
    private async Task<List<CategorySales>> CategorySalesAsync(ResolvedRange range, CancellationToken ct)
    {
        var totals =
            from i in SoldItems(range)
            join p in db.Set<Product>() on i.ProductId equals p.Id
            group i by p.CategoryId into g
            select new { CategoryId = g.Key, Quantity = g.Sum(i => i.Quantity), Sales = g.Sum(i => i.LineTotal) };

        return await (
            from t in totals
            join c in db.Set<Category>() on t.CategoryId equals c.Id
            orderby t.Sales descending, c.Name
            select new CategorySales(c.Id, c.Name, t.Quantity, t.Sales))
            .ToListAsync(ct);
    }

    /// <summary>Every order placed in the period, cancelled ones included, for the accounts export.</summary>
    public async Task<CsvBuilder> OrdersCsvAsync(ResolvedRange range, CancellationToken ct)
    {
        var orders = await Orders
            .Where(o => o.PlacedAtUtc >= range.FromUtc && o.PlacedAtUtc < range.ToUtcExclusive)
            .OrderBy(o => o.PlacedAtUtc).ThenBy(o => o.Id)
            .Select(o => new
            {
                o.Id, o.PlacedAtUtc, o.Status, o.PaymentStatus,
                Customer = db.Users.Where(u => u.Id == o.UserId).Select(u => u.FullName).First(),
                o.DeliveryAddress.PhoneNumber, o.DeliveryAddress.Pincode,
                Items = o.Items.Sum(i => i.Quantity),
                o.Subtotal, o.DeliveryFee, o.Total, o.CancellationReason,
            })
            .ToListAsync(ct);

        var csv = new CsvBuilder("Order", "Placed (IST)", "Status", "Payment", "Customer", "Phone", "PIN code",
            "Items", "Item total", "Delivery fee", "Order total", "Cancellation reason");
        foreach (var o in orders)
        {
            csv.Row(Order.NumberFor(o.Id), o.PlacedAtUtc, o.Status, o.PaymentStatus, o.Customer, o.PhoneNumber, o.Pincode,
                o.Items, o.Subtotal, o.DeliveryFee, o.Total, o.CancellationReason);
        }

        return csv;
    }

    // ---------- Inventory ----------

    public async Task<InventoryReportResponse> GetInventoryAsync(ResolvedRange range, CancellationToken ct)
    {
        var products = db.Set<Product>().Where(p => p.IsActive);
        var activeCount = await products.CountAsync(ct);
        var outOfStock = await products.CountAsync(p => p.StockQuantity == 0, ct);
        var lowStock = await products.CountAsync(p => p.StockQuantity > 0 && p.StockQuantity <= p.LowStockThreshold, ct);
        var stockValue = await products.SumAsync(p => p.StockQuantity * p.Price, ct);

        var adjustments = db.Set<InventoryAdjustment>()
            .Where(a => a.CreatedAtUtc >= range.FromUtc && a.CreatedAtUtc < range.ToUtcExclusive);

        var movements = await adjustments
            .GroupBy(a => a.Reason)
            .Select(g => new StockMovementSummary(g.Key, g.Count(), g.Sum(a => a.QuantityChange)))
            .ToListAsync(ct);

        var lost = await adjustments
            .Where(a => a.Reason == InventoryReason.Damaged || a.Reason == InventoryReason.Expired)
            .GroupBy(a => new { a.ProductId, a.Reason })
            .Select(g => new { g.Key.ProductId, g.Key.Reason, Units = -g.Sum(a => a.QuantityChange) })
            .ToListAsync(ct);
        var lostIds = lost.Select(l => l.ProductId).Distinct().ToList();
        var lostProducts = await db.Set<Product>().Where(p => lostIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Name, p.UnitLabel, p.Price })
            .ToDictionaryAsync(p => p.Id, ct);

        var lossRows = lost.GroupBy(l => l.ProductId)
            .Select(g =>
            {
                var p = lostProducts[g.Key];
                var damaged = g.Where(l => l.Reason == InventoryReason.Damaged).Sum(l => l.Units);
                var expired = g.Where(l => l.Reason == InventoryReason.Expired).Sum(l => l.Units);
                return new StockLoss(p.Id, p.Name, p.UnitLabel, damaged, expired, (damaged + expired) * p.Price);
            })
            .Where(l => l.Damaged + l.Expired != 0)
            .OrderByDescending(l => l.Value).ThenBy(l => l.Name)
            .ToList();

        return new InventoryReportResponse(
            range.From,
            range.To,
            activeCount,
            lowStock,
            outOfStock,
            stockValue,
            await ReorderListAsync(ct),
            [.. movements.OrderBy(m => m.Reason)],
            lossRows,
            lossRows.Sum(l => l.Value));
    }

    /// <summary>Active products at or below their low-stock level, emptiest first, with how fast they sell.</summary>
    public async Task<List<ReorderItem>> ReorderListAsync(CancellationToken ct)
    {
        var since = ShopTime.StartOfDayUtc(ShopTime.Today(clock).AddDays(-(SalesVelocityDays - 1)));
        var items = await db.Set<Product>()
            .Where(p => p.IsActive && p.StockQuantity <= p.LowStockThreshold)
            .OrderBy(p => p.StockQuantity).ThenBy(p => p.Name)
            .Select(p => new
            {
                p.Id, p.Name, p.Brand, p.UnitLabel, Category = p.Category.Name, p.StockQuantity, p.LowStockThreshold,
                // Sales minus cancellations: both are system entries in the stock ledger.
                Sold = -db.Set<InventoryAdjustment>()
                    .Where(a => a.ProductId == p.Id && a.CreatedAtUtc >= since
                        && (a.Reason == InventoryReason.Sale || a.Reason == InventoryReason.OrderCancelled))
                    .Sum(a => a.QuantityChange),
            })
            .ToListAsync(ct);

        return [.. items.Select(i => new ReorderItem(
            i.Id, i.Name, i.Brand, i.UnitLabel, i.Category, i.StockQuantity, i.LowStockThreshold, i.Sold,
            i.Sold > 0 ? Math.Round(i.StockQuantity / (i.Sold / (decimal)SalesVelocityDays), 1) : null))];
    }

    /// <summary>The raw stock ledger for the period, for audits.</summary>
    public async Task<CsvBuilder> StockMovementsCsvAsync(ResolvedRange range, CancellationToken ct)
    {
        var rows = await (
            from a in db.Set<InventoryAdjustment>()
            where a.CreatedAtUtc >= range.FromUtc && a.CreatedAtUtc < range.ToUtcExclusive
            join p in db.Set<Product>() on a.ProductId equals p.Id
            orderby a.CreatedAtUtc, a.Id
            select new
            {
                a.CreatedAtUtc, p.Name, p.UnitLabel, a.Reason, a.QuantityChange, a.QuantityAfter, a.Note,
                By = db.Users.Where(u => u.Id == a.CreatedByUserId).Select(u => u.FullName).FirstOrDefault(),
            })
            .ToListAsync(ct);

        var csv = new CsvBuilder("When (IST)", "Product", "Unit", "Reason", "Change", "Stock after", "Note", "By");
        foreach (var r in rows)
        {
            csv.Row(r.CreatedAtUtc, r.Name, r.UnitLabel, r.Reason, r.QuantityChange, r.QuantityAfter, r.Note, r.By);
        }

        return csv;
    }

    // ---------- Customers & wallet ----------

    public async Task<CustomerReportResponse> GetCustomersAsync(ResolvedRange range, CancellationToken ct)
    {
        var customerRoleId = db.Roles.Where(r => r.Name == Roles.Customer).Select(r => r.Id);
        var newCustomers = await db.Users
            .Where(u => u.CreatedAtUtc >= range.FromUtc && u.CreatedAtUtc < range.ToUtcExclusive)
            .CountAsync(u => db.UserRoles.Any(ur => ur.UserId == u.Id && customerRoleId.Contains(ur.RoleId)), ct);

        // For each customer who ordered in the period: when they first ever ordered.
        var buyers = await Sold
            .GroupBy(o => o.UserId)
            .Select(g => new
            {
                FirstOrderAtUtc = g.Min(o => o.PlacedAtUtc),
                InPeriod = g.Count(o => o.PlacedAtUtc >= range.FromUtc && o.PlacedAtUtc < range.ToUtcExclusive),
            })
            .Where(b => b.InPeriod > 0)
            .ToListAsync(ct);
        var firstTime = buyers.Count(b => b.FirstOrderAtUtc >= range.FromUtc);

        var wallets = db.Set<Wallet.Wallet>();
        var flows = await db.Set<WalletTransaction>()
            .Where(t => t.CreatedAtUtc >= range.FromUtc && t.CreatedAtUtc < range.ToUtcExclusive)
            .GroupBy(t => t.Type)
            .Select(g => new WalletFlow(g.Key, g.Count(), g.Sum(t => t.Amount)))
            .ToListAsync(ct);

        return new CustomerReportResponse(
            range.From,
            range.To,
            newCustomers,
            buyers.Count,
            firstTime,
            buyers.Count - firstTime,
            await CustomerSpendAsync(range, TopCount, ct),
            await wallets.SumAsync(w => w.Balance, ct),
            await wallets.CountAsync(w => w.Balance > 0, ct),
            [.. flows.OrderBy(f => f.Type)]);
    }

    /// <summary>Customers by what they spent in the period (non-cancelled orders), biggest first.</summary>
    public async Task<List<CustomerSpend>> CustomerSpendAsync(ResolvedRange range, int? top, CancellationToken ct)
    {
        var spend = Sold
            .Where(o => o.PlacedAtUtc >= range.FromUtc && o.PlacedAtUtc < range.ToUtcExclusive)
            .GroupBy(o => o.UserId)
            .Select(g => new { UserId = g.Key, Orders = g.Count(), Spent = g.Sum(o => o.Total), Last = g.Max(o => o.PlacedAtUtc) });

        var query =
            from s in spend
            join u in db.Users on s.UserId equals u.Id
            orderby s.Spent descending, u.FullName
            select new CustomerSpend(u.Id, u.FullName, u.PhoneNumber, s.Orders, s.Spent, s.Last);

        return await (top is { } n ? query.Take(n) : query).ToListAsync(ct);
    }
}

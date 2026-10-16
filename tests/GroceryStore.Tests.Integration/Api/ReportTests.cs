using System.Net;
using System.Net.Http.Json;
using GroceryStore.Api.Modules.Catalog;
using GroceryStore.Api.Modules.Identity;
using GroceryStore.Api.Modules.Orders;
using GroceryStore.Api.Modules.Reports;
using GroceryStore.Api.Modules.Wallet;
using GroceryStore.Tests.Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GroceryStore.Tests.Integration.Api;

/// <summary>
/// Report figures are checked exactly. The test database is shared with every other test, so each test
/// writes its orders and ledger rows directly into its own year in the past (2001, 2002, …), where nothing
/// else lives, and reports on that period only.
/// </summary>
public sealed class ReportTests(GroceryApiFactory factory)
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private static DateTime Utc(int year, int month, int day, int hour, int minute = 0) =>
        new(year, month, day, hour, minute, 0, DateTimeKind.Utc);

    private async Task<AdminProductResponse> ProductAsync(HttpClient admin, decimal price, int stock = 20, int lowStock = 5)
    {
        var category = await admin.CreateCategoryAsync(ct: _ct);
        return await admin.CreateProductAsync(
            CatalogTestData.NewProduct(category.Id) with { Price = price, Mrp = price + 10, InitialStock = stock, LowStockThreshold = lowStock }, _ct);
    }

    private async Task<int> CustomerIdAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<CurrentUserResponse>("/api/auth/me", _ct))!.Id;

    /// <summary>Writes an order as it would be stored at <paramref name="placedAtUtc"/> (₹25 delivery fee).</summary>
    private async Task<int> InsertOrderAsync(
        int userId, DateTime placedAtUtc, OrderStatus status, params (AdminProductResponse Product, int Quantity)[] lines)
    {
        var subtotal = lines.Sum(l => l.Product.Price * l.Quantity);
        var order = new Order
        {
            UserId = userId,
            Status = status,
            PaymentMethod = PaymentMethod.Wallet,
            PaymentStatus = status == OrderStatus.Cancelled ? PaymentStatus.Refunded : PaymentStatus.Paid,
            Subtotal = subtotal,
            MrpTotal = lines.Sum(l => l.Product.Mrp * l.Quantity),
            DeliveryFee = 25m,
            Total = subtotal + 25m,
            DeliveryAddress = new OrderAddress
            {
                RecipientName = "Report Test", PhoneNumber = "9876543210", Line1 = "1 Test Road",
                City = "Pune", State = "Maharashtra", Pincode = "411038",
            },
            CancellationReason = status == OrderStatus.Cancelled ? "Test cancellation" : null,
            CheckoutKey = Guid.NewGuid().ToString(),
            PlacedAtUtc = placedAtUtc,
            UpdatedAtUtc = placedAtUtc,
            Items = [.. lines.Select(l => new OrderItem
            {
                ProductId = l.Product.Id, ProductName = l.Product.Name, Brand = l.Product.Brand, UnitLabel = l.Product.UnitLabel,
                UnitPrice = l.Product.Price, UnitMrp = l.Product.Mrp, Quantity = l.Quantity, LineTotal = l.Product.Price * l.Quantity,
            })],
        };
        await using var db = factory.CreateDbContext();
        db.Add(order);
        await db.SaveChangesAsync(_ct);
        return order.Id;
    }

    private async Task InsertAsync(params object[] rows)
    {
        await using var db = factory.CreateDbContext();
        db.AddRange(rows);
        await db.SaveChangesAsync(_ct);
    }

    // ---------- Sales ----------

    [Fact]
    public async Task Sales_report_uses_ist_days_excludes_cancelled_orders_and_ranks_products()
    {
        using var admin = await factory.SignedInAdminAsync(_ct);
        var (customer, _) = await factory.SignedInCustomerAsync(_ct);
        var userId = await CustomerIdAsync(customer);
        var rice = await ProductAsync(admin, price: 100m);
        var salt = await ProductAsync(admin, price: 50m);

        // 18:45 UTC on 9 March is 00:15 IST on 10 March: it belongs to the 10th.
        await InsertOrderAsync(userId, Utc(2001, 3, 9, 18, 45), OrderStatus.Delivered, (rice, 2));
        await InsertOrderAsync(userId, Utc(2001, 3, 10, 18, 31), OrderStatus.Placed, (salt, 1)); // 00:01 IST on the 11th
        await InsertOrderAsync(userId, Utc(2001, 3, 10, 10, 0), OrderStatus.Cancelled, (rice, 1));
        await InsertOrderAsync(userId, Utc(2001, 3, 9, 18, 0), OrderStatus.Delivered, (salt, 10)); // 23:30 IST on the 9th: outside

        var report = await admin.GetFromJsonAsync<SalesReportResponse>("/api/admin/reports/sales?from=2001-03-10&to=2001-03-11", _ct);

        Assert.Equal(
            [new DailySales(new(2001, 3, 10), 1, 225m, 1), new DailySales(new(2001, 3, 11), 1, 75m, 0)],
            report!.Days);
        Assert.Equal(
            new SalesTotals(Orders: 2, Sales: 300m, ItemSales: 250m, DeliveryFees: 50m, CustomerSavings: 30m,
                AverageOrderValue: 150m, DeliveredSales: 225m, InProgressSales: 75m, CancelledOrders: 1, CancelledValue: 125m),
            report.Totals);
        Assert.Equal([(rice.Id, 2, 200m), (salt.Id, 1, 50m)], report.TopByRevenue.Select(p => (p.ProductId, p.Quantity, p.Sales)));
        Assert.Equal(rice.Id, report.TopByQuantity[0].ProductId);
        Assert.Equal([(rice.CategoryId, 200m), (salt.CategoryId, 50m)], report.Categories.Select(c => (c.CategoryId, c.Sales)));
    }

    [Fact]
    public async Task Sales_csv_has_a_row_for_every_day_and_opens_in_excel()
    {
        using var admin = await factory.SignedInAdminAsync(_ct);
        var (customer, _) = await factory.SignedInCustomerAsync(_ct);
        var product = await ProductAsync(admin, price: 100m);
        await InsertOrderAsync(await CustomerIdAsync(customer), Utc(2004, 6, 1, 6, 0), OrderStatus.Delivered, (product, 3));

        var response = await admin.GetAsync("/api/admin/reports/sales/daily.csv?from=2004-06-01&to=2004-06-03", _ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("daily-sales_2004-06-01_to_2004-06-03.csv", response.Content.Headers.ContentDisposition!.FileNameStar);
        var bytes = await response.Content.ReadAsByteArrayAsync(_ct);
        Assert.Equal([0xEF, 0xBB, 0xBF], bytes[..3]); // UTF-8 BOM: Excel then reads ₹ and Indian names correctly
        Assert.Equal(
            "Date,Orders,Sales,Cancelled orders\r\n2004-06-01,1,325.00,0\r\n2004-06-02,0,0.00,0\r\n2004-06-03,0,0.00,0\r\n",
            System.Text.Encoding.UTF8.GetString(bytes[3..]));
    }

    [Fact]
    public async Task Orders_csv_lists_every_order_and_defuses_spreadsheet_formulas_in_names()
    {
        using var admin = await factory.SignedInAdminAsync(_ct);
        var (customer, _) = await factory.SignedInCustomerAsync(_ct);
        var userId = await CustomerIdAsync(customer);
        await using (var db = factory.CreateDbContext())
        {
            var user = await db.Users.SingleAsync(u => u.Id == userId, _ct);
            user.FullName = "=HYPERLINK(\"http://evil\",\"Click\")";
            await db.SaveChangesAsync(_ct);
        }

        var product = await ProductAsync(admin, price: 100m);
        var id = await InsertOrderAsync(userId, Utc(2005, 1, 1, 4, 30), OrderStatus.Cancelled, (product, 1));

        var csv = await admin.GetStringAsync("/api/admin/reports/orders.csv?from=2005-01-01&to=2005-01-01", _ct);

        var row = csv.Split("\r\n")[1];
        Assert.StartsWith($"{Order.NumberFor(id)},2005-01-01 10:00,Cancelled,Refunded,\"'=HYPERLINK(\"\"http://evil\"\",\"\"Click\"\")\",", row);
        Assert.EndsWith(",1,100.00,25.00,125.00,Test cancellation", row);
    }

    // ---------- Inventory ----------

    [Fact]
    public async Task Inventory_report_shows_reorder_needs_movements_and_losses()
    {
        using var admin = await factory.SignedInAdminAsync(_ct);
        var product = await ProductAsync(admin, price: 40m, stock: 3, lowStock: 5);
        var now = DateTime.UtcNow;
        InventoryAdjustment Entry(InventoryReason reason, int change, DateTime at) => new()
        {
            ProductId = product.Id, QuantityChange = change, QuantityAfter = 0, Reason = reason, CreatedAtUtc = at,
        };
        await InsertAsync(
            Entry(InventoryReason.Damaged, -2, Utc(2002, 5, 1, 9)),
            Entry(InventoryReason.Expired, -1, Utc(2002, 5, 2, 9)),
            Entry(InventoryReason.Restock, 10, Utc(2002, 5, 3, 9)),
            Entry(InventoryReason.Damaged, -7, Utc(2002, 6, 1, 9)), // outside the period
            // Recent sales drive "days of stock left": 8 sold, 2 returned by a cancellation = 6 in 30 days.
            Entry(InventoryReason.Sale, -8, now.AddDays(-3)),
            Entry(InventoryReason.OrderCancelled, 2, now.AddDays(-2)));

        var report = await admin.GetFromJsonAsync<InventoryReportResponse>("/api/admin/reports/inventory?from=2002-05-01&to=2002-05-31", _ct);

        Assert.Equal(
            [new StockMovementSummary(InventoryReason.Restock, 1, 10), new StockMovementSummary(InventoryReason.Damaged, 1, -2),
             new StockMovementSummary(InventoryReason.Expired, 1, -1)],
            report!.Movements);
        Assert.Equal(new StockLoss(product.Id, product.Name, "1 kg", Damaged: 2, Expired: 1, Value: 120m), Assert.Single(report.Losses));
        Assert.Equal(120m, report.LossValue);

        var reorder = Assert.Single(report.Reorder, r => r.ProductId == product.Id);
        Assert.Equal((3, 5, 6, 15.0m), (reorder.StockQuantity, reorder.LowStockThreshold, reorder.SoldLast30Days, reorder.DaysOfCover));
        Assert.True(report.LowStockCount >= 1);

        var csv = await admin.GetStringAsync("/api/admin/reports/inventory/reorder.csv", _ct);
        Assert.Contains($"{product.Name},TestBrand,1 kg,{product.CategoryName},3,5,6,15.00", csv, StringComparison.Ordinal);
    }

    // ---------- Customers ----------

    [Fact]
    public async Task Customer_report_separates_first_time_and_returning_customers_and_sums_wallet_activity()
    {
        using var admin = await factory.SignedInAdminAsync(_ct);
        var product = await ProductAsync(admin, price: 100m);

        var (returning, _) = await factory.SignedInCustomerAsync(_ct);
        var (newcomer, _) = await factory.SignedInCustomerAsync(_ct);
        var (registeredOnly, _) = await factory.SignedInCustomerAsync(_ct);
        var returningId = await CustomerIdAsync(returning);
        var newcomerId = await CustomerIdAsync(newcomer);
        var registeredId = await CustomerIdAsync(registeredOnly);

        await InsertOrderAsync(returningId, Utc(2002, 12, 20, 6), OrderStatus.Delivered, (product, 1)); // before the period
        await InsertOrderAsync(returningId, Utc(2003, 1, 10, 6), OrderStatus.Delivered, (product, 1));
        await InsertOrderAsync(newcomerId, Utc(2003, 1, 5, 6), OrderStatus.Delivered, (product, 1));
        await InsertOrderAsync(newcomerId, Utc(2003, 1, 25, 6), OrderStatus.Packed, (product, 2));
        await InsertOrderAsync(newcomerId, Utc(2003, 1, 26, 6), OrderStatus.Cancelled, (product, 5)); // not counted

        await using (var db = factory.CreateDbContext())
        {
            (await db.Users.SingleAsync(u => u.Id == registeredId, _ct)).CreatedAtUtc = Utc(2003, 1, 15, 6);
            await db.SaveChangesAsync(_ct);
        }

        await returning.PostAsJsonAsync("/api/wallet/top-ups", new TopUpRequest(100m, Guid.NewGuid().ToString()), _ct); // creates the wallet
        WalletTransaction Tx(WalletTransactionType type, decimal amount, int day) => new()
        {
            UserId = returningId, Type = type, Amount = amount, BalanceAfter = 0, Description = "Report test",
            Reference = Guid.NewGuid().ToString(), CreatedAtUtc = Utc(2003, 1, day, 6),
        };
        await using (var db = factory.CreateDbContext())
        {
            db.AddRange(
                Tx(WalletTransactionType.TopUp, 500m, 2), Tx(WalletTransactionType.TopUp, 200m, 3),
                Tx(WalletTransactionType.OrderPayment, -125m, 10));
            // Keep the ledger invariant (balance = sum of entries) that WalletTests checks across the database.
            (await db.Set<Wallet>().SingleAsync(w => w.UserId == returningId, _ct)).Balance += 575m;
            await db.SaveChangesAsync(_ct);
        }

        var report = await admin.GetFromJsonAsync<CustomerReportResponse>("/api/admin/reports/customers?from=2003-01-01&to=2003-01-31", _ct);

        Assert.Equal((1, 2, 1, 1), (report!.NewCustomers, report.OrderingCustomers, report.FirstTimeCustomers, report.ReturningCustomers));
        Assert.Equal(
            [(newcomerId, 2, 350m), (returningId, 1, 125m)],
            report.TopCustomers.Select(c => (c.UserId, c.Orders, c.Spent)));
        Assert.Equal(
            [new WalletFlow(WalletTransactionType.TopUp, 2, 700m), new WalletFlow(WalletTransactionType.OrderPayment, 1, -125m)],
            report.WalletFlows);
        Assert.True(report.WalletBalanceTotal >= 100m);
    }

    // ---------- Dashboard ----------

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    /// <summary>
    /// "Today" is pinned to a day in 2006 so the figures are exact: other test classes place real orders
    /// today, in parallel, which would make live counts move under this test.
    /// </summary>
    [Fact]
    public async Task Dashboard_sales_use_the_ist_day_and_compare_with_yesterday()
    {
        using var admin = await factory.SignedInAdminAsync(_ct);
        var (customer, _) = await factory.SignedInCustomerAsync(_ct);
        var userId = await CustomerIdAsync(customer);
        var product = await ProductAsync(admin, price: 100m);

        await InsertOrderAsync(userId, Utc(2006, 7, 14, 19, 0), OrderStatus.Delivered, (product, 1)); // 00:30 IST on the 15th
        await InsertOrderAsync(userId, Utc(2006, 7, 15, 3, 0), OrderStatus.Placed, (product, 2));
        await InsertOrderAsync(userId, Utc(2006, 7, 15, 4, 0), OrderStatus.Cancelled, (product, 1));
        await InsertOrderAsync(userId, Utc(2006, 7, 14, 9, 0), OrderStatus.Delivered, (product, 1)); // yesterday

        using var scope = factory.Services.CreateScope();
        var reports = new ReportService(
            scope.ServiceProvider.GetRequiredService<GroceryStore.Api.Data.AppDbContext>(),
            scope.ServiceProvider.GetRequiredService<AdminOrderService>(),
            new FixedClock(new DateTimeOffset(2006, 7, 15, 10, 0, 0, TimeSpan.FromMinutes(330))));

        var dashboard = await reports.GetDashboardAsync(_ct);

        Assert.Equal(new DateOnly(2006, 7, 15), dashboard.Today);
        Assert.Equal(new SalesFigures(2, 350m, 175m), dashboard.TodaySales);
        Assert.Equal(new SalesFigures(1, 125m, 125m), dashboard.YesterdaySales);
        Assert.Equal(1, dashboard.CancelledToday);
        Assert.Equal(new DateOnly(2006, 7, 9), dashboard.Last7Days[0].Date);
    }

    [Fact]
    public async Task Dashboard_lists_the_open_queue_stock_alerts_and_latest_orders()
    {
        using var admin = await factory.SignedInAdminAsync(_ct);
        await ProductAsync(admin, price: 100m, stock: 0);

        var dashboard = (await admin.GetFromJsonAsync<DashboardResponse>("/api/admin/dashboard", _ct))!;

        Assert.Equal(7, dashboard.Last7Days.Count);
        Assert.Equal(dashboard.Today, dashboard.Last7Days[^1].Date);
        Assert.Equal(
            [OrderStatus.Placed, OrderStatus.Confirmed, OrderStatus.Packed, OrderStatus.OutForDelivery],
            dashboard.OpenOrders.Select(s => s.Status));
        Assert.True(dashboard.OutOfStockCount >= 1);
        Assert.Equal(0, dashboard.StockAlerts[0].StockQuantity); // emptiest first
        Assert.InRange(dashboard.RecentOrders.Count, 0, 5);
        Assert.Equal(dashboard.RecentOrders.OrderByDescending(o => o.PlacedAtUtc), dashboard.RecentOrders);
    }

    // ---------- Access and validation ----------

    [Theory]
    [InlineData("/api/admin/reports/sales?from=2026-02-01&to=2026-01-01")]
    [InlineData("/api/admin/reports/sales?from=2024-01-01&to=2025-12-31")]
    [InlineData("/api/admin/reports/customers?from=not-a-date")]
    public async Task Invalid_periods_are_refused(string url)
    {
        using var admin = await factory.SignedInAdminAsync(_ct);

        var response = await admin.GetAsync(url, _ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/admin/dashboard")]
    [InlineData("/api/admin/reports/sales")]
    [InlineData("/api/admin/reports/orders.csv")]
    public async Task Reports_are_admin_only(string url)
    {
        var (customer, _) = await factory.SignedInCustomerAsync(_ct);

        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync(url, _ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync(url, _ct)).StatusCode);
    }

    [Fact]
    public async Task Default_period_is_the_last_30_days_ending_today()
    {
        using var admin = await factory.SignedInAdminAsync(_ct);

        var report = await admin.GetFromJsonAsync<SalesReportResponse>("/api/admin/reports/sales", _ct);

        Assert.Equal(30, report!.Days.Count);
        Assert.Equal(report.From.AddDays(29), report.To);
    }
}

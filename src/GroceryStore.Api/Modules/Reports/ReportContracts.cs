using FluentValidation;
using GroceryStore.Api.Common;
using GroceryStore.Api.Modules.Catalog;
using GroceryStore.Api.Modules.Orders;
using GroceryStore.Api.Modules.Wallet;

namespace GroceryStore.Api.Modules.Reports;

/// <summary>
/// A report period in Indian calendar days, both ends included (?from=2026-10-01&amp;to=2026-10-31).
/// Missing dates default to the last 30 days, ending today.
/// </summary>
public sealed record ReportRange(DateOnly? From = null, DateOnly? To = null)
{
    public const int DefaultDays = 30;
    public const int MaxDays = 366;
}

internal sealed class ReportRangeValidator : AbstractValidator<ReportRange>
{
    public ReportRangeValidator()
    {
        RuleFor(r => r.To).GreaterThanOrEqualTo(r => r.From)
            .When(r => r.From is not null && r.To is not null)
            .WithMessage("The end date must be on or after the start date.");
        RuleFor(r => r.To)
            .Must((r, to) => r.From is null || to is null || to.Value.DayNumber - r.From.Value.DayNumber < ReportRange.MaxDays)
            .WithMessage($"Choose a period of at most {ReportRange.MaxDays} days.");
    }
}

/// <summary>A resolved period: the IST days shown to the user and the UTC instants used in queries.</summary>
public sealed record ResolvedRange(DateOnly From, DateOnly To)
{
    public DateTime FromUtc => ShopTime.StartOfDayUtc(From);

    /// <summary>Exclusive: the start of the day after <see cref="To"/>.</summary>
    public DateTime ToUtcExclusive => ShopTime.StartOfDayUtc(To.AddDays(1));

    public IEnumerable<DateOnly> Days => Enumerable.Range(0, To.DayNumber - From.DayNumber + 1).Select(From.AddDays);

    public string FileSuffix => $"{From:yyyy-MM-dd}_to_{To:yyyy-MM-dd}";
}

// ---------- Dashboard ----------

public sealed record SalesFigures(int Orders, decimal Sales, decimal AverageOrderValue);

/// <param name="Sales">Order totals (items + delivery) of orders placed that day, excluding cancelled ones.</param>
public sealed record DailySales(DateOnly Date, int Orders, decimal Sales, int CancelledOrders);

public sealed record StatusCount(OrderStatus Status, int Count);

public sealed record StockAlert(int ProductId, string Name, string? Brand, string UnitLabel, int StockQuantity, int LowStockThreshold);

public sealed record DashboardResponse(
    DateOnly Today,
    SalesFigures TodaySales,
    SalesFigures YesterdaySales,
    int CancelledToday,
    IReadOnlyList<StatusCount> OpenOrders,
    IReadOnlyList<DailySales> Last7Days,
    int OutOfStockCount,
    int LowStockCount,
    IReadOnlyList<StockAlert> StockAlerts,
    IReadOnlyList<AdminOrderSummaryResponse> RecentOrders);

// ---------- Sales ----------

/// <param name="Sales">What customers paid for non-cancelled orders: items + delivery fees.</param>
/// <param name="ItemSales">Item value only (order subtotals).</param>
/// <param name="CustomerSavings">MRP minus selling price on those items: the discount customers received.</param>
/// <param name="DeliveredSales">Part of <paramref name="Sales"/> already delivered; the rest is still in progress.</param>
/// <param name="CancelledValue">Total of cancelled orders, all refunded to wallets.</param>
public sealed record SalesTotals(
    int Orders,
    decimal Sales,
    decimal ItemSales,
    decimal DeliveryFees,
    decimal CustomerSavings,
    decimal AverageOrderValue,
    decimal DeliveredSales,
    decimal InProgressSales,
    int CancelledOrders,
    decimal CancelledValue);

public sealed record ProductSales(int ProductId, string Name, string? Brand, string UnitLabel, int Quantity, decimal Sales);

public sealed record CategorySales(int CategoryId, string Name, int Quantity, decimal Sales);

public sealed record SalesReportResponse(
    DateOnly From,
    DateOnly To,
    SalesTotals Totals,
    IReadOnlyList<DailySales> Days,
    IReadOnlyList<ProductSales> TopByRevenue,
    IReadOnlyList<ProductSales> TopByQuantity,
    IReadOnlyList<CategorySales> Categories);

// ---------- Inventory ----------

/// <param name="SoldLast30Days">Units sold (net of cancellations) in the 30 days up to today.</param>
/// <param name="DaysOfCover">How long the current stock lasts at that rate; null when nothing sold.</param>
public sealed record ReorderItem(
    int ProductId, string Name, string? Brand, string UnitLabel, string Category,
    int StockQuantity, int LowStockThreshold, int SoldLast30Days, decimal? DaysOfCover);

/// <param name="Units">Net change in units (negative = stock went out).</param>
public sealed record StockMovementSummary(InventoryReason Reason, int Entries, int Units);

/// <param name="Value">Units lost × current selling price (the shop has no cost prices yet).</param>
public sealed record StockLoss(int ProductId, string Name, string UnitLabel, int Damaged, int Expired, decimal Value);

public sealed record InventoryReportResponse(
    DateOnly From,
    DateOnly To,
    int ActiveProducts,
    int LowStockCount,
    int OutOfStockCount,
    decimal StockValueAtPrice,
    IReadOnlyList<ReorderItem> Reorder,
    IReadOnlyList<StockMovementSummary> Movements,
    IReadOnlyList<StockLoss> Losses,
    decimal LossValue);

// ---------- Customers & wallet ----------

public sealed record CustomerSpend(int UserId, string FullName, string? PhoneNumber, int Orders, decimal Spent, DateTime LastOrderAtUtc);

/// <param name="Amount">Signed sum: top-ups and refunds are positive, payments and debits negative.</param>
public sealed record WalletFlow(WalletTransactionType Type, int Count, decimal Amount);

/// <param name="FirstTimeCustomers">Ordering customers whose first ever order was in the period.</param>
/// <param name="WalletBalanceTotal">Money held in all wallets right now: what the shop owes its customers.</param>
public sealed record CustomerReportResponse(
    DateOnly From,
    DateOnly To,
    int NewCustomers,
    int OrderingCustomers,
    int FirstTimeCustomers,
    int ReturningCustomers,
    IReadOnlyList<CustomerSpend> TopCustomers,
    decimal WalletBalanceTotal,
    int CustomersWithBalance,
    IReadOnlyList<WalletFlow> WalletFlows);

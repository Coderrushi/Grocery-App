using GroceryStore.Api.Common;
using GroceryStore.Api.Modules.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GroceryStore.Api.Modules.Reports;

[ApiController]
[Route("api/admin/dashboard")]
[Authorize(Roles = Roles.Admin)]
[Produces("application/json")]
public sealed class AdminDashboardController(ReportService reports) : ControllerBase
{
    /// <summary>Today at a glance: sales so far (IST day), the open order queue, stock alerts and the latest orders.</summary>
    [HttpGet]
    public Task<DashboardResponse> Get(CancellationToken ct) => reports.GetDashboardAsync(ct);
}

/// <summary>
/// Reports for a period of IST days (?from=yyyy-MM-dd&amp;to=yyyy-MM-dd, default the last 30 days).
/// Each report has CSV downloads; they are plain GETs, so the browser can save them with a normal link.
/// </summary>
[ApiController]
[Route("api/admin/reports")]
[Authorize(Roles = Roles.Admin)]
public sealed class AdminReportsController(ReportService reports) : ControllerBase
{
    [HttpGet("sales")]
    [Produces("application/json")]
    public Task<SalesReportResponse> Sales([FromQuery] ReportRange range, CancellationToken ct) =>
        reports.GetSalesAsync(reports.Resolve(range), ct);

    [HttpGet("sales/daily.csv")]
    [Produces("text/csv")]
    public async Task<FileContentResult> SalesDailyCsv([FromQuery] ReportRange range, CancellationToken ct)
    {
        var period = reports.Resolve(range);
        var csv = new CsvBuilder("Date", "Orders", "Sales", "Cancelled orders");
        foreach (var day in await reports.DailySalesAsync(period, ct))
        {
            csv.Row(day.Date, day.Orders, day.Sales, day.CancelledOrders);
        }

        return csv.ToFile($"daily-sales_{period.FileSuffix}.csv");
    }

    [HttpGet("sales/products.csv")]
    [Produces("text/csv")]
    public async Task<FileContentResult> ProductSalesCsv([FromQuery] ReportRange range, CancellationToken ct)
    {
        var period = reports.Resolve(range);
        var csv = new CsvBuilder("Product", "Brand", "Unit", "Quantity sold", "Sales");
        foreach (var p in await reports.ProductSalesAsync(period, ct))
        {
            csv.Row(p.Name, p.Brand, p.UnitLabel, p.Quantity, p.Sales);
        }

        return csv.ToFile($"product-sales_{period.FileSuffix}.csv");
    }

    [HttpGet("orders.csv")]
    [Produces("text/csv")]
    public async Task<FileContentResult> OrdersCsv([FromQuery] ReportRange range, CancellationToken ct)
    {
        var period = reports.Resolve(range);
        return (await reports.OrdersCsvAsync(period, ct)).ToFile($"orders_{period.FileSuffix}.csv");
    }

    [HttpGet("inventory")]
    [Produces("application/json")]
    public Task<InventoryReportResponse> Inventory([FromQuery] ReportRange range, CancellationToken ct) =>
        reports.GetInventoryAsync(reports.Resolve(range), ct);

    /// <summary>Products to reorder now (not tied to a period).</summary>
    [HttpGet("inventory/reorder.csv")]
    [Produces("text/csv")]
    public async Task<FileContentResult> ReorderCsv(CancellationToken ct)
    {
        var csv = new CsvBuilder("Product", "Brand", "Unit", "Category", "In stock", "Low-stock level",
            "Sold in last 30 days", "Days of stock left");
        foreach (var i in await reports.ReorderListAsync(ct))
        {
            csv.Row(i.Name, i.Brand, i.UnitLabel, i.Category, i.StockQuantity, i.LowStockThreshold, i.SoldLast30Days, i.DaysOfCover);
        }

        return csv.ToFile("reorder-list.csv");
    }

    [HttpGet("inventory/movements.csv")]
    [Produces("text/csv")]
    public async Task<FileContentResult> StockMovementsCsv([FromQuery] ReportRange range, CancellationToken ct)
    {
        var period = reports.Resolve(range);
        return (await reports.StockMovementsCsvAsync(period, ct)).ToFile($"stock-movements_{period.FileSuffix}.csv");
    }

    [HttpGet("customers")]
    [Produces("application/json")]
    public Task<CustomerReportResponse> Customers([FromQuery] ReportRange range, CancellationToken ct) =>
        reports.GetCustomersAsync(reports.Resolve(range), ct);

    [HttpGet("customers.csv")]
    [Produces("text/csv")]
    public async Task<FileContentResult> CustomersCsv([FromQuery] ReportRange range, CancellationToken ct)
    {
        var period = reports.Resolve(range);
        var csv = new CsvBuilder("Customer", "Phone", "Orders", "Spent", "Last order (IST)");
        foreach (var c in await reports.CustomerSpendAsync(period, top: null, ct))
        {
            csv.Row(c.FullName, c.PhoneNumber, c.Orders, c.Spent, c.LastOrderAtUtc);
        }

        return csv.ToFile($"customer-spend_{period.FileSuffix}.csv");
    }
}

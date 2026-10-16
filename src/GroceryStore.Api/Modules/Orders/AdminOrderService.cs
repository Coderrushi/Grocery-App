using System.Globalization;
using System.Linq.Expressions;
using GroceryStore.Api.Common;
using GroceryStore.Api.Common.Errors;
using GroceryStore.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace GroceryStore.Api.Modules.Orders;

public sealed class AdminOrderService(AppDbContext db, OrderService orders)
{
    private IQueryable<Order> Orders => db.Set<Order>();

    public async Task<PagedResult<AdminOrderSummaryResponse>> ListAsync(AdminOrderQuery query, CancellationToken ct)
    {
        var source = Orders.AsQueryable();
        if (query.Status is { } status)
        {
            source = source.Where(o => o.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            // "QK-000042", "000042" and "42" all find order 42; otherwise match customer name or phone.
            var digits = new string([.. term.Where(char.IsAsciiDigit)]);
            var orderId = digits.Length is > 0 and <= 9 ? int.Parse(digits, CultureInfo.InvariantCulture) : -1;
            source = source.Where(o =>
                o.Id == orderId
                || o.DeliveryAddress.PhoneNumber.Contains(term)
                || o.DeliveryAddress.RecipientName.Contains(term)
                || db.Users.Any(u => u.Id == o.UserId && (u.FullName.Contains(term) || u.PhoneNumber!.Contains(term))));
        }

        var page = await source
            // Oldest first: the order that has waited longest is handled first.
            .OrderBy(o => o.PlacedAtUtc).ThenBy(o => o.Id)
            .Select(SummaryRow.Projection)
            .ToPagedResultAsync(query.Paging, ct);

        return page.Map(r => r.ToResponse());
    }

    /// <summary>The most recently placed orders, newest first (dashboard).</summary>
    public async Task<IReadOnlyList<AdminOrderSummaryResponse>> LatestAsync(int count, CancellationToken ct)
    {
        var rows = await Orders.OrderByDescending(o => o.PlacedAtUtc).ThenByDescending(o => o.Id)
            .Take(count)
            .Select(SummaryRow.Projection)
            .ToListAsync(ct);
        return [.. rows.Select(r => r.ToResponse())];
    }

    private sealed record SummaryRow(
        int Id, OrderStatus Status, DateTime PlacedAtUtc, decimal Total, int ItemCount, string RecipientName, string PhoneNumber, string Pincode)
    {
        public static readonly Expression<Func<Order, SummaryRow>> Projection = o => new SummaryRow(
            o.Id, o.Status, o.PlacedAtUtc, o.Total, o.Items.Sum(i => i.Quantity),
            o.DeliveryAddress.RecipientName, o.DeliveryAddress.PhoneNumber, o.DeliveryAddress.Pincode);

        public AdminOrderSummaryResponse ToResponse() => new(
            Id, Order.NumberFor(Id), Status, PlacedAtUtc, Total, ItemCount, RecipientName, PhoneNumber, Pincode);
    }

    public async Task<AdminOrderDetailResponse> GetAsync(int orderId, CancellationToken ct)
    {
        var order = await OrderService.LoadAsync(Orders, orderId, ct);
        var customer = await db.Users.Where(u => u.Id == order.UserId)
            .Select(u => new AdminOrderCustomer(u.Id, u.FullName, u.Email!, u.PhoneNumber))
            .SingleAsync(ct);

        return new AdminOrderDetailResponse(
            await orders.ToDetailAsync(order, customerView: false, ct),
            customer,
            OrderWorkflow.AllowedNext(order.Status),
            order.Version);
    }

    public async Task<AdminOrderDetailResponse> ChangeStatusAsync(int orderId, ChangeOrderStatusRequest request, int adminId, CancellationToken ct)
    {
        await orders.TransitionAsync(orderId, request.Status,
            string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(), adminId,
            ownerId: null, expectedVersion: request.Version, check: null, ct);

        return await GetAsync(orderId, ct);
    }

    /// <summary>
    /// Moves several orders forward at once, e.g. confirming the morning's new orders. Each order is its
    /// own transaction (cancellation stays one-at-a-time because it needs a reason the customer sees).
    /// Orders that can't move — a customer cancelled meanwhile, or it is already further along — are
    /// reported back rather than failing the whole batch.
    /// </summary>
    public async Task<BulkOrderStatusResponse> ChangeStatusBulkAsync(BulkOrderStatusRequest request, int adminId, CancellationToken ct)
    {
        List<int> updated = [];
        List<BulkOrderFailure> failed = [];
        foreach (var orderId in request.OrderIds.Distinct().Order())
        {
            try
            {
                await orders.TransitionAsync(orderId, request.Status, note: null, adminId,
                    ownerId: null, expectedVersion: null, check: null, ct);
                updated.Add(orderId);
            }
            catch (AppException ex)
            {
                failed.Add(new BulkOrderFailure(orderId, Order.NumberFor(orderId), ex.Message));
            }
            catch (DbUpdateConcurrencyException)
            {
                failed.Add(new BulkOrderFailure(orderId, Order.NumberFor(orderId), "Someone else updated this order at the same moment."));
            }
        }

        return new BulkOrderStatusResponse(updated, failed);
    }
}

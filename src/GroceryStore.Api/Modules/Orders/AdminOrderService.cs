using System.Globalization;
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
            .Select(o => new
            {
                o.Id, o.Status, o.PlacedAtUtc, o.Total,
                ItemCount = o.Items.Sum(i => i.Quantity),
                o.DeliveryAddress.RecipientName, o.DeliveryAddress.PhoneNumber, o.DeliveryAddress.Pincode,
            })
            .ToPagedResultAsync(query.Paging, ct);

        return page.Map(o => new AdminOrderSummaryResponse(
            o.Id, Order.NumberFor(o.Id), o.Status, o.PlacedAtUtc, o.Total, o.ItemCount, o.RecipientName, o.PhoneNumber, o.Pincode));
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
}

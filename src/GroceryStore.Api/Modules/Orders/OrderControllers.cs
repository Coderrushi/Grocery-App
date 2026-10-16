using GroceryStore.Api.Common;
using GroceryStore.Api.Common.Security;
using GroceryStore.Api.Modules.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GroceryStore.Api.Modules.Orders;

[ApiController]
[Authorize]
[Produces("application/json")]
public sealed class OrdersController(OrderService orders) : ControllerBase
{
    /// <summary>Cart, saved addresses (with delivery coverage) and wallet balance for the checkout page.</summary>
    [HttpGet("api/checkout")]
    public Task<CheckoutSummaryResponse> Checkout(CancellationToken ct) => orders.GetCheckoutSummaryAsync(User.GetUserId(), ct);

    /// <summary>
    /// Places an order for everything in the cart, paid from the wallet. Returns 409 if the cart or prices
    /// changed since checkout was opened, 422 if the wallet balance is too low. Safe to retry with the same key.
    /// </summary>
    [HttpPost("api/orders")]
    [ProducesResponseType<OrderDetailResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<OrderDetailResponse>> Place(PlaceOrderRequest request, CancellationToken ct)
    {
        var order = await orders.PlaceOrderAsync(User.GetUserId(), request, ct);
        return CreatedAtAction(nameof(Get), new { id = order.Id }, order);
    }

    [HttpGet("api/orders")]
    public Task<PagedResult<OrderSummaryResponse>> List([FromQuery] PageRequest page, CancellationToken ct) =>
        orders.ListAsync(User.GetUserId(), page, ct);

    [HttpGet("api/orders/{id:int}")]
    [ProducesResponseType<OrderDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<OrderDetailResponse> Get(int id, CancellationToken ct) => orders.GetAsync(User.GetUserId(), id, ct);

    /// <summary>Cancels the order while it is still "Placed". The payment is refunded to the wallet.</summary>
    [HttpPost("api/orders/{id:int}/cancel")]
    [ProducesResponseType<OrderDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<OrderDetailResponse> Cancel(int id, CancelOrderRequest request, CancellationToken ct) =>
        orders.CancelByCustomerAsync(User.GetUserId(), id, request.Reason, ct);
}

[ApiController]
[Route("api/admin/orders")]
[Authorize(Roles = Roles.Admin)]
[Produces("application/json")]
public sealed class AdminOrdersController(AdminOrderService orders) : ControllerBase
{
    /// <summary>Orders, oldest first, optionally filtered by status or searched by order number, name or phone.</summary>
    [HttpGet]
    public Task<PagedResult<AdminOrderSummaryResponse>> List([FromQuery] AdminOrderQuery query, CancellationToken ct) =>
        orders.ListAsync(query, ct);

    [HttpGet("{id:int}")]
    public Task<AdminOrderDetailResponse> Get(int id, CancellationToken ct) => orders.GetAsync(id, ct);

    /// <summary>Moves the order to its next status. Cancelling refunds the wallet and restocks the items.</summary>
    [HttpPost("{id:int}/status")]
    [ProducesResponseType<AdminOrderDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<AdminOrderDetailResponse> ChangeStatus(int id, ChangeOrderStatusRequest request, CancellationToken ct) =>
        orders.ChangeStatusAsync(id, request, User.GetUserId(), ct);

    /// <summary>
    /// Moves several orders to the same next status (e.g. confirm all new orders). Returns which ones moved
    /// and why the others could not; a partly successful batch is still 200.
    /// </summary>
    [HttpPost("bulk-status")]
    public Task<BulkOrderStatusResponse> ChangeStatusBulk(BulkOrderStatusRequest request, CancellationToken ct) =>
        orders.ChangeStatusBulkAsync(request, User.GetUserId(), ct);
}

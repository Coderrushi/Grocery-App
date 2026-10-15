using GroceryStore.Api.Common.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GroceryStore.Api.Modules.Cart;

[ApiController]
[Route("api/cart")]
[Authorize]
[Produces("application/json")]
public sealed class CartController(CartService cart) : ControllerBase
{
    [HttpGet]
    public Task<CartResponse> Get(CancellationToken ct) => cart.GetAsync(User.GetUserId(), ct);

    /// <summary>Sets the quantity of a product (0 removes it). Returns the whole updated cart.</summary>
    [HttpPut("items/{productId:int}")]
    [ProducesResponseType<CartResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<CartResponse> SetQuantity(int productId, SetCartQuantityRequest request, CancellationToken ct) =>
        cart.SetQuantityAsync(User.GetUserId(), productId, request.Quantity, ct);

    [HttpDelete("items/{productId:int}")]
    public Task<CartResponse> Remove(int productId, CancellationToken ct) => cart.RemoveAsync(User.GetUserId(), productId, ct);

    [HttpDelete]
    public Task<CartResponse> Clear(CancellationToken ct) => cart.ClearAsync(User.GetUserId(), ct);

    /// <summary>Adds the cart a visitor built before signing in. Never fails on unavailable items; it skips them.</summary>
    [HttpPost("merge")]
    public Task<CartResponse> Merge(GuestCartRequest request, CancellationToken ct) =>
        cart.MergeAsync(User.GetUserId(), request, ct);

    /// <summary>Prices a guest's cart (held in their browser) with current prices and stock. Stores nothing.</summary>
    [HttpPost("preview")]
    [AllowAnonymous]
    public Task<CartResponse> Preview(GuestCartRequest request, CancellationToken ct) => cart.PreviewAsync(request, ct);
}

using System.Text.Json.Serialization;
using FluentValidation;
using GroceryStore.Api.Modules.Catalog;

namespace GroceryStore.Api.Modules.Cart;

[JsonConverter(typeof(JsonStringEnumConverter<CartLineIssue>))]
public enum CartLineIssue
{
    /// <summary>The product was hidden or removed from the catalogue.</summary>
    Unavailable,

    OutOfStock,

    /// <summary>Fewer units can be bought than are in the cart (stock fell, or the per-order limit).</summary>
    ExceedsAvailable,
}

public sealed record CartLineResponse(
    int ProductId,
    string Name,
    string? Brand,
    string UnitLabel,
    string? ImageUrl,
    string? CategoryIcon,
    decimal UnitPrice,
    decimal UnitMrp,
    int Quantity,
    decimal LineTotal,
    // Most this customer can buy right now: min(MaxPerOrder, stock). 0 when unavailable.
    int MaxQuantity,
    StockStatus StockStatus,
    CartLineIssue? Issue);

/// <summary>Lines with an issue are shown but excluded from the totals, because they can't be bought as they are.</summary>
public sealed record CartResponse(
    IReadOnlyList<CartLineResponse> Lines,
    CartTotals Totals,
    bool HasIssues,
    bool CanCheckout);

public sealed record SetCartQuantityRequest(int Quantity);

internal sealed class SetCartQuantityRequestValidator : AbstractValidator<SetCartQuantityRequest>
{
    public SetCartQuantityRequestValidator() =>
        RuleFor(r => r.Quantity).InclusiveBetween(0, 100).WithMessage("Choose a quantity between 0 and 100.");
}

public sealed record CartLineRequest(int ProductId, int Quantity);

/// <summary>A browser-held (guest) cart: priced via /preview, or merged into the account at sign-in.</summary>
public sealed record GuestCartRequest(IReadOnlyList<CartLineRequest> Items);

internal sealed class GuestCartRequestValidator : AbstractValidator<GuestCartRequest>
{
    public GuestCartRequestValidator()
    {
        RuleFor(r => r.Items).NotNull().Must(items => items.Count <= CartService.MaxLines)
            .WithMessage($"A cart can hold at most {CartService.MaxLines} different products.");
        RuleForEach(r => r.Items).ChildRules(line =>
        {
            line.RuleFor(l => l.ProductId).GreaterThan(0);
            line.RuleFor(l => l.Quantity).InclusiveBetween(1, 100);
        });
    }
}

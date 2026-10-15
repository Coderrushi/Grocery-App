using System.ComponentModel.DataAnnotations;

namespace GroceryStore.Api.Modules.Cart;

/// <summary>
/// Delivery charge rules (appsettings "DeliveryCharges"). Phase 7 moves these into admin-editable
/// store settings; until then they are configuration.
/// </summary>
public sealed class DeliveryChargesOptions
{
    public const string SectionName = "DeliveryCharges";

    [Range(0, 1000)]
    public decimal DeliveryFee { get; set; } = 25m;

    /// <summary>Orders with an item total at or above this are delivered free.</summary>
    [Range(0, 100_000)]
    public decimal FreeDeliveryThreshold { get; set; } = 299m;

    /// <summary>Smallest item total the shop accepts for delivery.</summary>
    [Range(0, 100_000)]
    public decimal MinimumOrderAmount { get; set; } = 99m;
}

public sealed record CartTotals(
    int ItemCount,
    decimal Subtotal,
    decimal MrpTotal,
    decimal Savings,
    decimal DeliveryFee,
    decimal Total,
    decimal AmountToFreeDelivery,
    decimal MinimumOrderAmount,
    bool MeetsMinimumOrder);

/// <summary>
/// THE pricing rule for a basket. Pure and side-effect free, so it is unit-tested exhaustively and
/// reused unchanged by checkout (Phase 6), which must charge exactly what the cart showed.
/// </summary>
public static class CartPricing
{
    public static CartTotals Calculate(IEnumerable<(decimal Price, decimal Mrp, int Quantity)> lines, DeliveryChargesOptions rules)
    {
        var itemCount = 0;
        var subtotal = 0m;
        var mrpTotal = 0m;
        foreach (var (price, mrp, quantity) in lines)
        {
            itemCount += quantity;
            subtotal += price * quantity;
            mrpTotal += mrp * quantity;
        }

        var deliveryFee = subtotal == 0 || subtotal >= rules.FreeDeliveryThreshold ? 0m : rules.DeliveryFee;
        var amountToFreeDelivery = subtotal > 0 && subtotal < rules.FreeDeliveryThreshold
            ? rules.FreeDeliveryThreshold - subtotal
            : 0m;

        return new CartTotals(
            itemCount,
            subtotal,
            mrpTotal,
            Savings: mrpTotal - subtotal,
            deliveryFee,
            Total: subtotal + deliveryFee,
            amountToFreeDelivery,
            rules.MinimumOrderAmount,
            MeetsMinimumOrder: subtotal >= rules.MinimumOrderAmount);
    }
}

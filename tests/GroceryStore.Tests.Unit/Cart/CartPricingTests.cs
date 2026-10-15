using GroceryStore.Api.Modules.Cart;

namespace GroceryStore.Tests.Unit.Cart;

public sealed class CartPricingTests
{
    private static readonly DeliveryChargesOptions Rules = new()
    {
        DeliveryFee = 25m,
        FreeDeliveryThreshold = 299m,
        MinimumOrderAmount = 99m,
    };

    [Fact]
    public void Totals_add_up_line_prices_mrp_and_savings()
    {
        var totals = CartPricing.Calculate([(90m, 100m, 2), (28m, 28m, 1)], Rules);

        Assert.Equal(3, totals.ItemCount);
        Assert.Equal(208m, totals.Subtotal);
        Assert.Equal(228m, totals.MrpTotal);
        Assert.Equal(20m, totals.Savings);
        Assert.Equal(25m, totals.DeliveryFee);
        Assert.Equal(233m, totals.Total);
        Assert.Equal(91m, totals.AmountToFreeDelivery);
        Assert.True(totals.MeetsMinimumOrder);
    }

    [Theory]
    [InlineData(298.99, 25, 0.01)] // one paisa short: still charged
    [InlineData(299, 0, 0)] // exactly the threshold: free
    [InlineData(500, 0, 0)]
    public void Delivery_is_free_from_the_threshold(decimal subtotal, decimal expectedFee, decimal expectedToFree)
    {
        var totals = CartPricing.Calculate([(subtotal, subtotal, 1)], Rules);

        Assert.Equal(expectedFee, totals.DeliveryFee);
        Assert.Equal(expectedToFree, totals.AmountToFreeDelivery);
        Assert.Equal(subtotal + expectedFee, totals.Total);
    }

    [Theory]
    [InlineData(98.99, false)]
    [InlineData(99, true)]
    public void Minimum_order_is_inclusive(decimal subtotal, bool meets) =>
        Assert.Equal(meets, CartPricing.Calculate([(subtotal, subtotal, 1)], Rules).MeetsMinimumOrder);

    [Fact]
    public void An_empty_cart_costs_nothing_and_has_no_delivery_fee()
    {
        var totals = CartPricing.Calculate([], Rules);

        Assert.Equal(0, totals.ItemCount);
        Assert.Equal(0m, totals.DeliveryFee);
        Assert.Equal(0m, totals.Total);
        Assert.Equal(0m, totals.AmountToFreeDelivery);
        Assert.False(totals.MeetsMinimumOrder);
    }

    [Fact]
    public void Money_stays_exact_with_paise()
    {
        // 3 x 33.33 must be exactly 99.99, never 99.98999...
        var totals = CartPricing.Calculate([(33.33m, 33.33m, 3)], Rules);
        Assert.Equal(99.99m, totals.Subtotal);
    }
}

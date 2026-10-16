using GroceryStore.Api.Modules.Orders;

namespace GroceryStore.Tests.Unit.Orders;

public sealed class OrderWorkflowTests
{
    [Theory]
    [InlineData(OrderStatus.Placed, OrderStatus.Confirmed)]
    [InlineData(OrderStatus.Confirmed, OrderStatus.Packed)]
    [InlineData(OrderStatus.Packed, OrderStatus.OutForDelivery)]
    [InlineData(OrderStatus.OutForDelivery, OrderStatus.Delivered)]
    [InlineData(OrderStatus.Placed, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.OutForDelivery, OrderStatus.Cancelled)] // failed delivery
    public void Allowed_moves(OrderStatus from, OrderStatus to) => Assert.True(OrderWorkflow.CanMove(from, to));

    [Theory]
    [InlineData(OrderStatus.Placed, OrderStatus.Delivered)] // no skipping steps
    [InlineData(OrderStatus.Packed, OrderStatus.Confirmed)] // no going back
    [InlineData(OrderStatus.Delivered, OrderStatus.Cancelled)] // delivered is final
    [InlineData(OrderStatus.Cancelled, OrderStatus.Placed)] // cancelled is final
    [InlineData(OrderStatus.Placed, OrderStatus.Placed)]
    public void Forbidden_moves(OrderStatus from, OrderStatus to) => Assert.False(OrderWorkflow.CanMove(from, to));

    [Fact]
    public void Customers_can_cancel_only_before_the_store_confirms() =>
        Assert.Equal(
            [OrderStatus.Placed],
            Enum.GetValues<OrderStatus>().Where(OrderWorkflow.CustomerCanCancel));

    [Fact]
    public void Every_status_has_a_rule_and_only_delivered_and_cancelled_are_final() =>
        Assert.Equal(
            [OrderStatus.Delivered, OrderStatus.Cancelled],
            Enum.GetValues<OrderStatus>().Where(OrderWorkflow.IsFinal));

    [Fact]
    public void Order_numbers_are_zero_padded() => Assert.Equal("QK-000042", Order.NumberFor(42));
}

namespace GroceryStore.Api.Modules.Orders;

/// <summary>
/// The order lifecycle in one place. Every status change in the app is checked against this table,
/// so adding a step (e.g. "Ready for pickup") is a change here plus a migration-free enum value.
/// </summary>
public static class OrderWorkflow
{
    private static readonly Dictionary<OrderStatus, OrderStatus[]> Next = new()
    {
        [OrderStatus.Placed] = [OrderStatus.Confirmed, OrderStatus.Cancelled],
        [OrderStatus.Confirmed] = [OrderStatus.Packed, OrderStatus.Cancelled],
        [OrderStatus.Packed] = [OrderStatus.OutForDelivery, OrderStatus.Cancelled],
        // A failed delivery (customer unreachable, refused) is cancelled; the goods come back to the shop.
        [OrderStatus.OutForDelivery] = [OrderStatus.Delivered, OrderStatus.Cancelled],
        [OrderStatus.Delivered] = [],
        [OrderStatus.Cancelled] = [],
    };

    public static IReadOnlyList<OrderStatus> AllowedNext(OrderStatus current) => Next[current];

    public static bool CanMove(OrderStatus from, OrderStatus to) => Next[from].Contains(to);

    /// <summary>Customers may cancel only until the shop has confirmed (i.e. started working on) the order.</summary>
    public static bool CustomerCanCancel(OrderStatus current) => current == OrderStatus.Placed;

    public static bool IsFinal(OrderStatus status) => Next[status].Length == 0;
}

using GroceryStore.Api.Common;
using GroceryStore.Api.Common.Errors;
using GroceryStore.Api.Data;
using GroceryStore.Api.Modules.Cart;
using GroceryStore.Api.Modules.Catalog;
using GroceryStore.Api.Modules.Customers;
using GroceryStore.Api.Modules.Wallet;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MySql.Data.MySqlClient;

namespace GroceryStore.Api.Modules.Orders;

public sealed class OrderService(
    AppDbContext db,
    CartService cart,
    AddressService addresses,
    InventoryService inventory,
    WalletService wallet,
    IOptions<DeliveryAreaOptions> deliveryArea,
    TimeProvider timeProvider)
{
    private const int MySqlDuplicateKeyError = 1062;

    private IQueryable<Order> OrdersOf(int userId) => db.Set<Order>().Where(o => o.UserId == userId);

    // ---------- Checkout ----------

    public async Task<CheckoutSummaryResponse> GetCheckoutSummaryAsync(int userId, CancellationToken ct)
    {
        var priced = await cart.GetAsync(userId, ct);
        var saved = await addresses.ListAsync(userId, ct);
        var balance = await wallet.GetBalanceAsync(userId, ct);
        return new CheckoutSummaryResponse(
            priced,
            [.. saved.Select(a => new CheckoutAddressResponse(a, deliveryArea.Value.Serves(a.Pincode)))],
            balance,
            HasEnoughBalance: balance >= priced.Totals.Total);
    }

    /// <summary>
    /// Places the order. Re-pricing, stock reduction, payment, order creation and emptying the cart all
    /// happen in ONE transaction: either the customer has a paid order and the stock is reserved, or
    /// nothing changed at all.
    /// </summary>
    public async Task<OrderDetailResponse> PlaceOrderAsync(int userId, PlaceOrderRequest request, CancellationToken ct)
    {
        if (await FindByCheckoutKeyAsync(userId, request.CheckoutKey, ct) is { } existing)
        {
            return await GetAsync(userId, existing, ct); // a double tap or retry: same order, no second charge
        }

        var address = await addresses.GetAsync(userId, request.AddressId, ct); // 404 if not theirs
        if (!deliveryArea.Value.Serves(address.Pincode))
        {
            throw new BusinessRuleException($"Sorry, we don't deliver to PIN code {address.Pincode} yet.");
        }

        int orderId;
        try
        {
            orderId = await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                // A retried attempt (e.g. after a deadlock) must start from a clean slate.
                db.ChangeTracker.Clear();
                await using var transaction = await db.Database.BeginTransactionAsync(ct);

                var lines = await db.Set<CartItem>().Where(i => i.UserId == userId)
                    .Select(i => new CartLineRequest(i.ProductId, i.Quantity)).ToListAsync(ct);
                if (lines.Count == 0)
                {
                    throw new BusinessRuleException("Your cart is empty.");
                }

                var priced = await cart.PriceAsync(lines, ct);
                if (!priced.CanCheckout)
                {
                    throw new ConflictException(priced.HasIssues
                        ? "Some items in your cart have changed. Please review your cart."
                        : $"The minimum order is {Money.Format(priced.Totals.MinimumOrderAmount)}.");
                }

                if (priced.Totals.Total != request.ExpectedTotal)
                {
                    throw new ConflictException(
                        $"Prices have changed since you opened checkout. The new total is {Money.Format(priced.Totals.Total)}. Please review and try again.");
                }

                var now = timeProvider.GetUtcNow().UtcDateTime;
                var order = new Order
                {
                    UserId = userId,
                    Status = OrderStatus.Placed,
                    PaymentMethod = PaymentMethod.Wallet,
                    PaymentStatus = PaymentStatus.Paid,
                    Subtotal = priced.Totals.Subtotal,
                    MrpTotal = priced.Totals.MrpTotal,
                    DeliveryFee = priced.Totals.DeliveryFee,
                    Total = priced.Totals.Total,
                    DeliveryAddress = ToOrderAddress(address),
                    DeliveryInstructions = string.IsNullOrWhiteSpace(request.DeliveryInstructions) ? null : request.DeliveryInstructions.Trim(),
                    CheckoutKey = request.CheckoutKey,
                    PlacedAtUtc = now,
                    UpdatedAtUtc = now,
                    StatusHistory = [new OrderStatusChange { ToStatus = OrderStatus.Placed, ChangedByUserId = userId, ChangedAtUtc = now }],
                };
                // 1. The order header first: it gives us the order number, and a duplicate CheckoutKey
                //    (concurrent double tap) fails here, before anything else is touched.
                db.Add(order);
                await db.SaveChangesAsync(ct);

                var number = Order.NumberFor(order.Id);

                // 2. Reserve stock BEFORE inserting order items. Item rows reference products, and MySQL's
                //    foreign-key check takes a shared lock on each product row; if two checkouts did that and
                //    then both tried to update the same stock row, they would deadlock. Updating first takes
                //    the exclusive lock up front, always in product-id order so checkouts queue rather than
                //    cross. Each update refuses to take stock below zero.
                foreach (var line in priced.Lines.OrderBy(l => l.ProductId))
                {
                    try
                    {
                        await inventory.ApplyAsync(line.ProductId, -line.Quantity, InventoryReason.Sale, number, userId, ct);
                    }
                    catch (BusinessRuleException)
                    {
                        throw new ConflictException($"Sorry, {line.Name} just sold out. Please review your cart.");
                    }
                }

                // 3. The items: a snapshot of what was bought, at the price paid.
                order.Items.AddRange(priced.Lines.Select(l => new OrderItem
                {
                    ProductId = l.ProductId,
                    ProductName = l.Name,
                    Brand = l.Brand,
                    UnitLabel = l.UnitLabel,
                    UnitPrice = l.UnitPrice,
                    UnitMrp = l.UnitMrp,
                    Quantity = l.Quantity,
                    LineTotal = l.LineTotal,
                }));
                await db.SaveChangesAsync(ct);

                // 4. Payment. Insufficient balance throws here and the whole transaction rolls back.
                await wallet.DebitAsync(userId, order.Total, WalletTransactionType.OrderPayment,
                    $"Payment for order {number}", reference: $"order:{order.Id}:payment", createdByUserId: null, ct);

                await db.Set<CartItem>().Where(i => i.UserId == userId).ExecuteDeleteAsync(ct);

                await transaction.CommitAsync(ct);
                return order.Id;
            });
        }
        catch (DbUpdateException e) when (e.InnerException is MySqlException { Number: MySqlDuplicateKeyError })
        {
            // The same checkout attempt was submitted twice at once and the other request won.
            db.ChangeTracker.Clear();
            orderId = await FindByCheckoutKeyAsync(userId, request.CheckoutKey, ct)
                ?? throw new ConflictException("Please try placing your order again.");
        }

        return await GetAsync(userId, orderId, ct);
    }

    private Task<int?> FindByCheckoutKeyAsync(int userId, string key, CancellationToken ct) =>
        OrdersOf(userId).Where(o => o.CheckoutKey == key).Select(o => (int?)o.Id).SingleOrDefaultAsync(ct);

    private static OrderAddress ToOrderAddress(AddressResponse a) => new()
    {
        RecipientName = a.RecipientName,
        PhoneNumber = a.PhoneNumber,
        Line1 = a.Line1,
        Line2 = a.Line2,
        Landmark = a.Landmark,
        City = a.City,
        State = a.State,
        Pincode = a.Pincode,
    };

    // ---------- Customer queries ----------

    public async Task<PagedResult<OrderSummaryResponse>> ListAsync(int userId, PageRequest page, CancellationToken ct)
    {
        var result = await OrdersOf(userId)
            .OrderByDescending(o => o.PlacedAtUtc).ThenByDescending(o => o.Id)
            .Select(o => new
            {
                o.Id, o.Status, o.PlacedAtUtc, o.Total,
                ItemCount = o.Items.Sum(i => i.Quantity),
                Names = o.Items.OrderBy(i => i.Id).Select(i => i.ProductName).Take(3).ToList(),
                Lines = o.Items.Count,
            })
            .ToPagedResultAsync(page, ct);

        return result.Map(o => new OrderSummaryResponse(
            o.Id, Order.NumberFor(o.Id), o.Status, o.PlacedAtUtc, o.Total, o.ItemCount,
            string.Join(", ", o.Names) + (o.Lines > o.Names.Count ? $" +{o.Lines - o.Names.Count} more" : "")));
    }

    public async Task<OrderDetailResponse> GetAsync(int userId, int orderId, CancellationToken ct)
    {
        var order = await LoadAsync(OrdersOf(userId), orderId, ct);
        return await ToDetailAsync(order, customerView: true, ct);
    }

    // ---------- Cancellation ----------

    public async Task<OrderDetailResponse> CancelByCustomerAsync(int userId, int orderId, string? reason, CancellationToken ct)
    {
        await TransitionAsync(orderId, OrderStatus.Cancelled,
            string.IsNullOrWhiteSpace(reason) ? "Cancelled by customer" : reason.Trim(), userId,
            ownerId: userId, expectedVersion: null,
            check: order =>
            {
                if (!OrderWorkflow.CustomerCanCancel(order.Status))
                {
                    throw new BusinessRuleException(order.Status switch
                    {
                        OrderStatus.Cancelled => "This order is already cancelled.",
                        OrderStatus.Delivered => "This order has already been delivered. For a problem with it, please call the store.",
                        _ => "The store has already started preparing this order. Please call the store to cancel it.",
                    });
                }
            },
            ct);
        return await GetAsync(userId, orderId, ct);
    }

    /// <summary>
    /// Moves an order to a new status, enforcing the workflow. Cancelling also refunds the payment to the
    /// wallet and returns the stock, in the same transaction.
    /// The order is (re)loaded INSIDE the retryable transaction, so an automatic retry after a transient
    /// failure replays everything from a clean state. Its Version guards against two people changing
    /// the same order at once (the second gets 409).
    /// </summary>
    /// <param name="ownerId">Restricts to this customer's orders (customer actions); null for staff.</param>
    /// <param name="expectedVersion">The version the admin was looking at; null to skip that check.</param>
    internal Task TransitionAsync(
        int orderId, OrderStatus to, string? note, int changedByUserId, int? ownerId, int? expectedVersion,
        Action<Order>? check, CancellationToken ct) =>
        db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            var source = ownerId is { } owner ? OrdersOf(owner) : db.Set<Order>();
            var order = await source.Include(o => o.Items).SingleOrDefaultAsync(o => o.Id == orderId, ct)
                ?? throw new NotFoundException("Order not found.");

            if (expectedVersion is { } version && order.Version != version)
            {
                throw new ConflictException("This order was updated by someone else. Reload it to see the latest status.");
            }

            check?.Invoke(order);
            if (!OrderWorkflow.CanMove(order.Status, to))
            {
                throw new BusinessRuleException($"An order that is {order.Status} can't be moved to {to}.");
            }

            var now = timeProvider.GetUtcNow().UtcDateTime;
            db.Add(new OrderStatusChange
            {
                OrderId = order.Id,
                FromStatus = order.Status,
                ToStatus = to,
                Note = note,
                ChangedByUserId = changedByUserId,
                ChangedAtUtc = now,
            });
            order.Status = to;
            order.UpdatedAtUtc = now;
            order.Version++;

            if (to == OrderStatus.Cancelled)
            {
                order.CancellationReason = note;
                order.PaymentStatus = PaymentStatus.Refunded;
            }

            await db.SaveChangesAsync(ct); // version check: fails with 409 if someone else changed it first

            if (to == OrderStatus.Cancelled)
            {
                var number = Order.NumberFor(order.Id);
                foreach (var item in order.Items.OrderBy(i => i.ProductId))
                {
                    await inventory.ApplyAsync(item.ProductId, item.Quantity, InventoryReason.OrderCancelled, number, changedByUserId, ct);
                }

                await wallet.CreditAsync(order.UserId, order.Total, WalletTransactionType.OrderRefund,
                    $"Refund for cancelled order {number}", reference: $"order:{order.Id}:refund",
                    createdByUserId: null, maxBalance: null, ct);
            }

            await transaction.CommitAsync(ct);
        });

    // ---------- Shared loading / mapping ----------

    /// <summary>Read-only load of an order with its items and timeline.</summary>
    internal static async Task<Order> LoadAsync(IQueryable<Order> source, int orderId, CancellationToken ct) =>
        await source.Include(o => o.Items).Include(o => o.StatusHistory).AsSplitQuery().AsNoTracking()
            .SingleOrDefaultAsync(o => o.Id == orderId, ct) ?? throw new NotFoundException("Order not found.");

    internal async Task<OrderDetailResponse> ToDetailAsync(Order o, bool customerView, CancellationToken ct)
    {
        var userIds = o.StatusHistory.Where(c => c.ChangedByUserId != null).Select(c => c.ChangedByUserId!.Value).Distinct().ToList();
        var names = await db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        // Customers see "You" or "QuickKirana" rather than individual staff names.
        string? Who(int? id) => id is null ? null
            : customerView ? (id == o.UserId ? "You" : "Store")
            : names.GetValueOrDefault(id.Value);

        return new OrderDetailResponse(
            o.Id, Order.NumberFor(o.Id), o.Status, o.PaymentMethod, o.PaymentStatus, o.PlacedAtUtc,
            [.. o.Items.OrderBy(i => i.Id).Select(i => new OrderItemResponse(
                i.ProductId, i.ProductName, i.Brand, i.UnitLabel, i.UnitPrice, i.UnitMrp, i.Quantity, i.LineTotal))],
            o.Subtotal, o.MrpTotal, o.MrpTotal - o.Subtotal, o.DeliveryFee, o.Total,
            o.DeliveryAddress, o.DeliveryInstructions, o.CancellationReason,
            [.. o.StatusHistory.OrderBy(c => c.ChangedAtUtc).ThenBy(c => c.Id).Select(c => new OrderStatusChangeResponse(
                c.FromStatus, c.ToStatus, c.Note, Who(c.ChangedByUserId), c.ChangedAtUtc))],
            CanCancel: OrderWorkflow.CustomerCanCancel(o.Status));
    }
}

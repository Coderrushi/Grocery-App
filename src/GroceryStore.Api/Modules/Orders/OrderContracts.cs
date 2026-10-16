using FluentValidation;
using GroceryStore.Api.Common;
using GroceryStore.Api.Modules.Cart;
using GroceryStore.Api.Modules.Customers;

namespace GroceryStore.Api.Modules.Orders;

/// <summary>Where the shop delivers (appsettings "DeliveryArea"). An empty list means everywhere.</summary>
public sealed class DeliveryAreaOptions
{
    public const string SectionName = "DeliveryArea";

    public string[] ServiceablePincodes { get; set; } = [];

    public bool Serves(string pincode) => ServiceablePincodes.Length == 0 || ServiceablePincodes.Contains(pincode);
}

// ---------- Checkout ----------

public sealed record CheckoutAddressResponse(AddressResponse Address, bool IsServiceable);

/// <summary>Everything the checkout page needs, in one call.</summary>
public sealed record CheckoutSummaryResponse(
    CartResponse Cart,
    IReadOnlyList<CheckoutAddressResponse> Addresses,
    decimal WalletBalance,
    bool HasEnoughBalance);

/// <param name="ExpectedTotal">The total the customer saw. If prices changed since, the order is refused (409).</param>
/// <param name="CheckoutKey">A fresh random id per attempt; repeating it returns the same order.</param>
public sealed record PlaceOrderRequest(int AddressId, string? DeliveryInstructions, decimal ExpectedTotal, string CheckoutKey);

internal sealed class PlaceOrderRequestValidator : AbstractValidator<PlaceOrderRequest>
{
    public PlaceOrderRequestValidator()
    {
        RuleFor(r => r.AddressId).GreaterThan(0).WithMessage("Choose a delivery address.");
        RuleFor(r => r.DeliveryInstructions).MaximumLength(200);
        RuleFor(r => r.ExpectedTotal).GreaterThan(0);
        RuleFor(r => r.CheckoutKey).NotEmpty().MaximumLength(64);
    }
}

// ---------- Orders ----------

public sealed record OrderItemResponse(
    int ProductId, string Name, string? Brand, string UnitLabel, decimal UnitPrice, decimal UnitMrp, int Quantity, decimal LineTotal);

public sealed record OrderStatusChangeResponse(OrderStatus? FromStatus, OrderStatus ToStatus, string? Note, string? ChangedBy, DateTime ChangedAtUtc);

public sealed record OrderSummaryResponse(
    int Id, string OrderNumber, OrderStatus Status, DateTime PlacedAtUtc, decimal Total, int ItemCount, string ItemsPreview);

public sealed record OrderDetailResponse(
    int Id,
    string OrderNumber,
    OrderStatus Status,
    PaymentMethod PaymentMethod,
    PaymentStatus PaymentStatus,
    DateTime PlacedAtUtc,
    IReadOnlyList<OrderItemResponse> Items,
    decimal Subtotal,
    decimal MrpTotal,
    decimal Savings,
    decimal DeliveryFee,
    decimal Total,
    OrderAddress DeliveryAddress,
    string? DeliveryInstructions,
    string? CancellationReason,
    IReadOnlyList<OrderStatusChangeResponse> Timeline,
    bool CanCancel);

public sealed record CancelOrderRequest(string? Reason);

internal sealed class CancelOrderRequestValidator : AbstractValidator<CancelOrderRequest>
{
    public CancelOrderRequestValidator() => RuleFor(r => r.Reason).MaximumLength(200);
}

// ---------- Admin ----------

public sealed record AdminOrderSummaryResponse(
    int Id, string OrderNumber, OrderStatus Status, DateTime PlacedAtUtc, decimal Total, int ItemCount,
    string CustomerName, string CustomerPhone, string Pincode);

public sealed record AdminOrderCustomer(int Id, string FullName, string Email, string? PhoneNumber);

public sealed record AdminOrderDetailResponse(
    OrderDetailResponse Order,
    AdminOrderCustomer Customer,
    IReadOnlyList<OrderStatus> AllowedNextStatuses,
    int Version);

public sealed record AdminOrderQuery(OrderStatus? Status = null, string? Search = null, int Page = 1, int PageSize = 20)
{
    public PageRequest Paging => new(Page, PageSize);
}

/// <param name="Version">The order version the admin was looking at; a stale version returns 409.</param>
public sealed record ChangeOrderStatusRequest(OrderStatus Status, string? Note, int Version);

internal sealed class ChangeOrderStatusRequestValidator : AbstractValidator<ChangeOrderStatusRequest>
{
    public ChangeOrderStatusRequestValidator()
    {
        RuleFor(r => r.Status).IsInEnum();
        RuleFor(r => r.Note).MaximumLength(200);
        RuleFor(r => r.Note).NotEmpty().When(r => r.Status == OrderStatus.Cancelled)
            .WithMessage("Give a reason for cancelling. The customer will see it.");
    }
}

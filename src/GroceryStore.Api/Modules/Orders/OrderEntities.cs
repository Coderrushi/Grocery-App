using System.Text.Json.Serialization;
using GroceryStore.Api.Modules.Catalog;
using GroceryStore.Api.Modules.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GroceryStore.Api.Modules.Orders;

[JsonConverter(typeof(JsonStringEnumConverter<OrderStatus>))]
public enum OrderStatus
{
    Placed,
    Confirmed,
    Packed,
    OutForDelivery,
    Delivered,
    Cancelled,
}

[JsonConverter(typeof(JsonStringEnumConverter<PaymentMethod>))]
public enum PaymentMethod
{
    Wallet, // cash on delivery / online payment can be added as further values
}

[JsonConverter(typeof(JsonStringEnumConverter<PaymentStatus>))]
public enum PaymentStatus
{
    Paid,
    Refunded,
}

/// <summary>Where the order goes, copied from the customer's saved address at checkout.</summary>
public class OrderAddress
{
    public string RecipientName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Line1 { get; set; } = string.Empty;
    public string? Line2 { get; set; }
    public string? Landmark { get; set; }
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Pincode { get; set; } = string.Empty;
}

/// <summary>
/// A placed order. Everything needed to show, deliver or refund it is copied in at checkout
/// (address, names, prices), so later catalogue or address edits never change what was bought.
/// </summary>
public class Order
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public OrderStatus Status { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public PaymentStatus PaymentStatus { get; set; }

    public decimal Subtotal { get; set; }
    public decimal MrpTotal { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal Total { get; set; }

    public OrderAddress DeliveryAddress { get; set; } = new();
    public string? DeliveryInstructions { get; set; }
    public string? CancellationReason { get; set; }

    /// <summary>Client-generated per checkout attempt; a repeated "Place order" returns the same order.</summary>
    public string CheckoutKey { get; set; } = string.Empty;

    /// <summary>Optimistic concurrency: two simultaneous status changes can't both succeed.</summary>
    public int Version { get; set; }

    public DateTime PlacedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public List<OrderItem> Items { get; set; } = [];
    public List<OrderStatusChange> StatusHistory { get; set; } = [];

    /// <summary>Human-friendly order number shown to customers and staff, e.g. "QK-000042".</summary>
    public static string NumberFor(int id) => $"QK-{id:D6}";
}

public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string UnitLabel { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public decimal UnitMrp { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }
}

/// <summary>The order's timeline: every status it has been in, when, and who moved it.</summary>
public class OrderStatusChange
{
    public long Id { get; set; }
    public int OrderId { get; set; }
    public OrderStatus? FromStatus { get; set; }
    public OrderStatus ToStatus { get; set; }
    public string? Note { get; set; }
    public int? ChangedByUserId { get; set; }
    public DateTime ChangedAtUtc { get; set; }
}

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders", t =>
        {
            t.HasCheckConstraint("CK_orders_total", "`Total` = `Subtotal` + `DeliveryFee`");
            t.HasCheckConstraint("CK_orders_amounts_not_negative", "`Subtotal` >= 0 AND `DeliveryFee` >= 0");
        });
        builder.HasOne<AppUser>().WithMany().HasForeignKey(o => o.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(o => o.PaymentMethod).HasConversion<string>().HasMaxLength(20);
        builder.Property(o => o.PaymentStatus).HasConversion<string>().HasMaxLength(20);
        builder.Property(o => o.DeliveryInstructions).HasMaxLength(200);
        builder.Property(o => o.CancellationReason).HasMaxLength(200);
        builder.Property(o => o.CheckoutKey).HasMaxLength(64);
        builder.Property(o => o.Version).IsConcurrencyToken();

        // The address is stored in the orders table itself (Delivery* columns): an order has exactly one.
        builder.OwnsOne(o => o.DeliveryAddress, address =>
        {
            address.Property(a => a.RecipientName).HasColumnName("DeliveryRecipientName").HasMaxLength(100);
            address.Property(a => a.PhoneNumber).HasColumnName("DeliveryPhoneNumber").HasMaxLength(10);
            address.Property(a => a.Line1).HasColumnName("DeliveryLine1").HasMaxLength(150);
            address.Property(a => a.Line2).HasColumnName("DeliveryLine2").HasMaxLength(150);
            address.Property(a => a.Landmark).HasColumnName("DeliveryLandmark").HasMaxLength(100);
            address.Property(a => a.City).HasColumnName("DeliveryCity").HasMaxLength(60);
            address.Property(a => a.State).HasColumnName("DeliveryState").HasMaxLength(60);
            address.Property(a => a.Pincode).HasColumnName("DeliveryPincode").HasMaxLength(6).IsFixedLength();
        });
        builder.Navigation(o => o.DeliveryAddress).IsRequired();

        builder.HasIndex(o => new { o.UserId, o.CheckoutKey }).IsUnique();
        builder.HasIndex(o => new { o.UserId, o.PlacedAtUtc }); // "my orders", newest first
        builder.HasIndex(o => new { o.Status, o.PlacedAtUtc }); // admin queue by status
    }
}

internal sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("order_items", t => t.HasCheckConstraint("CK_order_items_quantity_positive", "`Quantity` > 0"));
        builder.HasOne<Order>().WithMany(o => o.Items).HasForeignKey(i => i.OrderId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Product>().WithMany().HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(i => i.ProductName).HasMaxLength(150);
        builder.Property(i => i.Brand).HasMaxLength(60);
        builder.Property(i => i.UnitLabel).HasMaxLength(30);
    }
}

internal sealed class OrderStatusChangeConfiguration : IEntityTypeConfiguration<OrderStatusChange>
{
    public void Configure(EntityTypeBuilder<OrderStatusChange> builder)
    {
        builder.ToTable("order_status_changes");
        builder.HasOne<Order>().WithMany(o => o.StatusHistory).HasForeignKey(c => c.OrderId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(c => c.ChangedByUserId).OnDelete(DeleteBehavior.SetNull);
        builder.Property(c => c.FromStatus).HasConversion<string>().HasMaxLength(20);
        builder.Property(c => c.ToStatus).HasConversion<string>().HasMaxLength(20);
        builder.Property(c => c.Note).HasMaxLength(200);
    }
}

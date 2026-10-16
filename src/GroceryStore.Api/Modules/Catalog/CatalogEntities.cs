using System.Text.Json.Serialization;
using GroceryStore.Api.Modules.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GroceryStore.Api.Modules.Catalog;

/// <summary>A store aisle, e.g. "Dairy & Eggs". One level deep for now.</summary>
public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>URL key, e.g. "dairy-eggs". Fixed at creation so shared links keep working after a rename.</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>An emoji shown on the category tile, e.g. "🥛".</summary>
    public string? Icon { get; set; }

    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public class Product
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string? Description { get; set; }

    /// <summary>What one unit is, as printed on the shelf: "1 kg", "500 ml", "Pack of 6".</summary>
    public string UnitLabel { get; set; } = string.Empty;

    /// <summary>Optional shop code / barcode.</summary>
    public string? Sku { get; set; }

    /// <summary>Selling price in INR, inclusive of taxes.</summary>
    public decimal Price { get; set; }

    /// <summary>Maximum Retail Price printed on the pack. Always &gt;= Price.</summary>
    public decimal Mrp { get; set; }

    /// <summary>Units on hand. Changed only through <see cref="InventoryAdjustment"/>s (and, later, orders).</summary>
    public int StockQuantity { get; set; }

    /// <summary>At or below this level customers see "Only N left" and the admin sees a low-stock warning.</summary>
    public int LowStockThreshold { get; set; } = 5;

    /// <summary>Most units one customer may buy in a single order (keeps scarce items fair).</summary>
    public int MaxPerOrder { get; set; } = 10;

    public string? ImagePath { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsFeatured { get; set; }

    /// <summary>Optimistic concurrency token for admin edits. Stock updates deliberately do not change it.</summary>
    public int Version { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter<InventoryReason>))]
public enum InventoryReason
{
    InitialStock,
    Restock,
    Correction,
    Damaged,
    Expired,
    Returned,

    // System-only reasons, written by checkout and cancellation (never chosen by staff):
    Sale,
    OrderCancelled,
}

/// <summary>
/// An append-only stock ledger entry. Every change to a product's stock is recorded with who did it and
/// why, so the shopkeeper can always explain the current number.
/// </summary>
public class InventoryAdjustment
{
    public long Id { get; set; }
    public int ProductId { get; set; }
    public int QuantityChange { get; set; }
    public int QuantityAfter { get; set; }
    public InventoryReason Reason { get; set; }
    public string? Note { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories");
        builder.Property(c => c.Name).HasMaxLength(60);
        builder.Property(c => c.Slug).HasMaxLength(80);
        builder.Property(c => c.Icon).HasMaxLength(16);
        builder.Property(c => c.Description).HasMaxLength(300);
        builder.HasIndex(c => c.Name).IsUnique();
        builder.HasIndex(c => c.Slug).IsUnique();
    }
}

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products", table =>
        {
            // Last line of defence: the database itself refuses impossible values, whatever the code does.
            table.HasCheckConstraint("CK_products_stock_not_negative", "`StockQuantity` >= 0");
            table.HasCheckConstraint("CK_products_mrp_at_least_price", "`Mrp` >= `Price`");
        });
        builder.HasOne(p => p.Category).WithMany().HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict); // a category with products cannot be deleted
        builder.Property(p => p.Name).HasMaxLength(150);
        builder.Property(p => p.Brand).HasMaxLength(60);
        builder.Property(p => p.Description).HasMaxLength(2000);
        builder.Property(p => p.UnitLabel).HasMaxLength(30);
        builder.Property(p => p.Sku).HasMaxLength(40);
        builder.Property(p => p.ImagePath).HasMaxLength(200);
        builder.Property(p => p.Version).IsConcurrencyToken();

        builder.HasIndex(p => p.Sku).IsUnique();
        // Storefront listing: active products of a category, ordered by name.
        builder.HasIndex(p => new { p.CategoryId, p.IsActive, p.Name });
        builder.HasIndex(p => new { p.IsActive, p.IsFeatured });
    }
}

internal sealed class InventoryAdjustmentConfiguration : IEntityTypeConfiguration<InventoryAdjustment>
{
    public void Configure(EntityTypeBuilder<InventoryAdjustment> builder)
    {
        builder.ToTable("inventory_adjustments");
        builder.HasOne<Product>().WithMany().HasForeignKey(a => a.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(a => a.CreatedByUserId).OnDelete(DeleteBehavior.SetNull);
        builder.Property(a => a.Reason).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.Note).HasMaxLength(200);
        builder.HasIndex(a => new { a.ProductId, a.CreatedAtUtc });
    }
}

using System.Linq.Expressions;
using GroceryStore.Api.Common.Media;

namespace GroceryStore.Api.Modules.Catalog;

/// <summary>The columns a product tile needs, loaded in one SQL projection (no full entity tracking).</summary>
internal sealed record ProductRow(
    int Id,
    string Name,
    string? Brand,
    string UnitLabel,
    decimal Price,
    decimal Mrp,
    string? ImagePath,
    string CategorySlug,
    string? CategoryIcon,
    int StockQuantity,
    int LowStockThreshold,
    int MaxPerOrder);

/// <summary>Pricing and stock rules shared by the storefront and admin views.</summary>
internal static class ProductProjections
{
    public static readonly Expression<Func<Product, ProductRow>> ToRow = p => new ProductRow(
        p.Id, p.Name, p.Brand, p.UnitLabel, p.Price, p.Mrp, p.ImagePath, p.Category.Slug, p.Category.Icon,
        p.StockQuantity, p.LowStockThreshold, p.MaxPerOrder);

    public static ProductCardResponse ToCard(ProductRow r, IMediaStorage media)
    {
        var status = StatusOf(r.StockQuantity, r.LowStockThreshold);
        return new ProductCardResponse(
            r.Id, r.Name, r.Brand, r.UnitLabel, r.Price, r.Mrp, DiscountPercent(r.Price, r.Mrp),
            r.ImagePath is null ? null : media.PublicUrl(r.ImagePath),
            r.CategorySlug, r.CategoryIcon, status,
            OnlyLeft: status == StockStatus.LowStock ? r.StockQuantity : null,
            MaxQuantity: Math.Max(0, Math.Min(r.MaxPerOrder, r.StockQuantity)));
    }

    /// <summary>Rounded down, so the badge never overstates the saving.</summary>
    public static int DiscountPercent(decimal price, decimal mrp) =>
        mrp > price && mrp > 0 ? (int)Math.Floor((mrp - price) / mrp * 100) : 0;

    public static StockStatus StatusOf(int stock, int lowStockThreshold) => stock switch
    {
        <= 0 => StockStatus.OutOfStock,
        _ when stock <= lowStockThreshold => StockStatus.LowStock,
        _ => StockStatus.InStock,
    };
}

using System.Text.Json.Serialization;

namespace GroceryStore.Api.Modules.Catalog;

// ---------- Storefront (anonymous) ----------

public sealed record CategoryResponse(int Id, string Name, string Slug, string? Icon, string? Description, int ProductCount);

[JsonConverter(typeof(JsonStringEnumConverter<StockStatus>))]
public enum StockStatus
{
    InStock,
    LowStock,
    OutOfStock,
}

/// <summary>A product tile in a grid or list.</summary>
public sealed record ProductCardResponse(
    int Id,
    string Name,
    string? Brand,
    string UnitLabel,
    decimal Price,
    decimal Mrp,
    int DiscountPercent,
    string? ImageUrl,
    string CategorySlug,
    string? CategoryIcon,
    StockStatus StockStatus,
    // Set only when stock is low, for "Only 3 left".
    int? OnlyLeft,
    // Most a customer can put in the cart right now: min(MaxPerOrder, stock).
    int MaxQuantity);

public sealed record ProductDetailResponse(
    ProductCardResponse Product,
    string? Description,
    string CategoryName);

[JsonConverter(typeof(JsonStringEnumConverter<ProductSort>))]
public enum ProductSort
{
    Relevance,
    PriceLowToHigh,
    PriceHighToLow,
    Name,
    Newest,
    BiggestDiscount,
}

public sealed record ProductQuery(
    string? Category = null,
    string? Search = null,
    bool Featured = false,
    bool InStockOnly = false,
    ProductSort Sort = ProductSort.Relevance,
    int Page = 1,
    int PageSize = 24);

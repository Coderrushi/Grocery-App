using System.Text.Json.Serialization;
using FluentValidation;

namespace GroceryStore.Api.Modules.Catalog;

// ---------- Categories ----------

public sealed record AdminCategoryResponse(
    int Id, string Name, string Slug, string? Icon, string? Description, int SortOrder, bool IsActive, int ProductCount);

public sealed record SaveCategoryRequest(string Name, string? Icon, string? Description, int SortOrder, bool IsActive);

internal sealed class SaveCategoryRequestValidator : AbstractValidator<SaveCategoryRequest>
{
    public SaveCategoryRequestValidator()
    {
        RuleFor(r => r.Name).NotEmpty().WithMessage("Enter a category name.").MaximumLength(60);
        RuleFor(r => r.Icon).MaximumLength(16);
        RuleFor(r => r.Description).MaximumLength(300);
        RuleFor(r => r.SortOrder).InclusiveBetween(0, 1000);
    }
}

// ---------- Products ----------

public sealed record AdminProductResponse(
    int Id,
    int CategoryId,
    string CategoryName,
    string Name,
    string? Brand,
    string? Description,
    string UnitLabel,
    string? Sku,
    decimal Price,
    decimal Mrp,
    int DiscountPercent,
    int StockQuantity,
    int LowStockThreshold,
    int MaxPerOrder,
    StockStatus StockStatus,
    string? ImageUrl,
    bool IsActive,
    bool IsFeatured,
    int Version,
    DateTime UpdatedAtUtc);

/// <summary>Fields shared by create and update, validated by one set of rules.</summary>
public interface IProductFields
{
    int CategoryId { get; }
    string Name { get; }
    string? Brand { get; }
    string? Description { get; }
    string UnitLabel { get; }
    string? Sku { get; }
    decimal Price { get; }
    decimal Mrp { get; }
    int LowStockThreshold { get; }
    int MaxPerOrder { get; }
    bool IsActive { get; }
    bool IsFeatured { get; }
}

public sealed record CreateProductRequest(
    int CategoryId, string Name, string? Brand, string? Description, string UnitLabel, string? Sku,
    decimal Price, decimal Mrp, int LowStockThreshold, int MaxPerOrder, bool IsActive, bool IsFeatured,
    int InitialStock) : IProductFields;

public sealed record UpdateProductRequest(
    int CategoryId, string Name, string? Brand, string? Description, string UnitLabel, string? Sku,
    decimal Price, decimal Mrp, int LowStockThreshold, int MaxPerOrder, bool IsActive, bool IsFeatured,
    int Version) : IProductFields;

internal sealed class ProductFieldsValidator : AbstractValidator<IProductFields>
{
    public const decimal MaxPrice = 100_000m;

    public ProductFieldsValidator()
    {
        RuleFor(r => r.CategoryId).GreaterThan(0).WithMessage("Select a category.");
        RuleFor(r => r.Name).NotEmpty().WithMessage("Enter the product name.").MaximumLength(150);
        RuleFor(r => r.Brand).MaximumLength(60);
        RuleFor(r => r.Description).MaximumLength(2000);
        RuleFor(r => r.UnitLabel).NotEmpty().WithMessage("Enter the unit, e.g. \"1 kg\" or \"500 ml\".").MaximumLength(30);
        RuleFor(r => r.Sku).MaximumLength(40);
        RuleFor(r => r.Price)
            .GreaterThan(0).WithMessage("Enter a price above ₹0.")
            .LessThanOrEqualTo(MaxPrice)
            .PrecisionScale(10, 2, ignoreTrailingZeros: true).WithMessage("Use at most 2 decimal places (paise).");
        RuleFor(r => r.Mrp)
            .GreaterThanOrEqualTo(r => r.Price).WithMessage("MRP can't be lower than the selling price.")
            .LessThanOrEqualTo(MaxPrice)
            .PrecisionScale(10, 2, ignoreTrailingZeros: true).WithMessage("Use at most 2 decimal places (paise).");
        RuleFor(r => r.LowStockThreshold).InclusiveBetween(0, 1000);
        RuleFor(r => r.MaxPerOrder).InclusiveBetween(1, 100);
    }
}

internal sealed class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductRequestValidator()
    {
        Include(new ProductFieldsValidator());
        RuleFor(r => r.InitialStock).InclusiveBetween(0, 100_000);
    }
}

internal sealed class UpdateProductRequestValidator : AbstractValidator<UpdateProductRequest>
{
    public UpdateProductRequestValidator() => Include(new ProductFieldsValidator());
}

[JsonConverter(typeof(JsonStringEnumConverter<ProductStatusFilter>))]
public enum ProductStatusFilter
{
    All,
    Active,
    Inactive,
}

[JsonConverter(typeof(JsonStringEnumConverter<StockFilter>))]
public enum StockFilter
{
    All,
    Low,
    Out,
}

public sealed record AdminProductQuery(
    string? Search = null,
    int? CategoryId = null,
    ProductStatusFilter Status = ProductStatusFilter.All,
    StockFilter Stock = StockFilter.All,
    int Page = 1,
    int PageSize = 20);

// ---------- Inventory ----------

public sealed record AdjustStockRequest(int QuantityChange, InventoryReason Reason, string? Note);

internal sealed class AdjustStockRequestValidator : AbstractValidator<AdjustStockRequest>
{
    public AdjustStockRequestValidator()
    {
        RuleFor(r => r.QuantityChange).NotEqual(0).WithMessage("Enter how many units to add or remove.")
            .InclusiveBetween(-100_000, 100_000);
        RuleFor(r => r.Reason).IsInEnum()
            .NotEqual(InventoryReason.InitialStock).WithMessage("Initial stock is set when the product is created.");
        RuleFor(r => r.QuantityChange).GreaterThan(0)
            .When(r => r.Reason is InventoryReason.Restock or InventoryReason.Returned)
            .WithMessage("A restock or return adds units, so the quantity must be positive.");
        RuleFor(r => r.QuantityChange).LessThan(0)
            .When(r => r.Reason is InventoryReason.Damaged or InventoryReason.Expired)
            .WithMessage("Damaged or expired stock removes units, so the quantity must be negative.");
        RuleFor(r => r.Note).MaximumLength(200);
    }
}

public sealed record InventoryAdjustmentResponse(
    long Id, int QuantityChange, int QuantityAfter, InventoryReason Reason, string? Note, string? CreatedBy, DateTime CreatedAtUtc);

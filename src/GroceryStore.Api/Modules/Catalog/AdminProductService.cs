using FluentValidation;
using FluentValidation.Results;
using GroceryStore.Api.Common;
using GroceryStore.Api.Common.Errors;
using GroceryStore.Api.Common.Media;
using GroceryStore.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace GroceryStore.Api.Modules.Catalog;

public sealed class AdminProductService(
    AppDbContext db,
    InventoryService inventory,
    IMediaStorage media,
    TimeProvider timeProvider)
{
    private const string ImageFolder = "products";

    private DbSet<Product> Products => db.Set<Product>();

    public async Task<PagedResult<AdminProductResponse>> ListAsync(AdminProductQuery query, CancellationToken ct)
    {
        var products = Products.AsQueryable();

        foreach (var term in CatalogQueryService.SearchTerms(query.Search))
        {
            products = products.Where(p =>
                p.Name.Contains(term) || (p.Brand != null && p.Brand.Contains(term)) || (p.Sku != null && p.Sku == term));
        }

        if (query.CategoryId is { } categoryId)
        {
            products = products.Where(p => p.CategoryId == categoryId);
        }

        products = query.Status switch
        {
            ProductStatusFilter.Active => products.Where(p => p.IsActive),
            ProductStatusFilter.Inactive => products.Where(p => !p.IsActive),
            _ => products,
        };

        products = query.Stock switch
        {
            StockFilter.Low => products.Where(p => p.StockQuantity > 0 && p.StockQuantity <= p.LowStockThreshold),
            StockFilter.Out => products.Where(p => p.StockQuantity <= 0),
            _ => products,
        };

        var page = await products
            .OrderBy(p => p.Name).ThenBy(p => p.Id)
            .Select(p => new { Product = p, CategoryName = p.Category.Name })
            .ToPagedResultAsync(new PageRequest(query.Page, query.PageSize), ct);

        return page.Map(x => ToResponse(x.Product, x.CategoryName));
    }

    public async Task<AdminProductResponse> GetAsync(int id, CancellationToken ct)
    {
        var product = await Products.Include(p => p.Category).AsNoTracking().SingleOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException("Product not found.");
        return ToResponse(product, product.Category.Name);
    }

    public async Task<AdminProductResponse> CreateAsync(CreateProductRequest request, int userId, CancellationToken ct)
    {
        await ValidateReferencesAsync(request, productId: null, ct);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var product = new Product { CreatedAtUtc = now };
        Apply(request, product, now);

        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            Products.Add(product);
            await db.SaveChangesAsync(ct);
            if (request.InitialStock > 0)
            {
                await inventory.ApplyAsync(product.Id, request.InitialStock, InventoryReason.InitialStock, null, userId, ct);
            }

            await transaction.CommitAsync(ct);
        });

        return await GetAsync(product.Id, ct);
    }

    /// <summary>
    /// Saves the admin's edit only if nobody else changed the product since they loaded it: the update is
    /// conditional on the Version they were shown. Otherwise the global handler returns 409 Conflict.
    /// Stock is NOT part of this update; use stock adjustments.
    /// </summary>
    public async Task<AdminProductResponse> UpdateAsync(int id, UpdateProductRequest request, CancellationToken ct)
    {
        var product = await Products.SingleOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException("Product not found.");
        await ValidateReferencesAsync(request, productId: id, ct);

        db.Entry(product).Property(p => p.Version).OriginalValue = request.Version;
        Apply(request, product, timeProvider.GetUtcNow().UtcDateTime);
        product.Version = request.Version + 1;
        await db.SaveChangesAsync(ct);

        return await GetAsync(id, ct);
    }

    public async Task<AdminProductResponse> SetImageAsync(int id, IFormFile file, CancellationToken ct)
    {
        var product = await Products.SingleOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException("Product not found.");

        if (file.Length is 0 or > ImageFile.MaxBytes)
        {
            throw ImageError($"Choose an image smaller than {ImageFile.MaxBytes / (1024 * 1024)} MB.");
        }

        await using var stream = file.OpenReadStream();
        var header = new byte[12];
        var read = await stream.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, ct);
        var extension = ImageFile.DetectExtension(header.AsSpan(0, read))
            ?? throw ImageError("Upload a JPEG, PNG or WebP image.");
        stream.Position = 0;

        var newPath = await media.SaveAsync(stream, ImageFolder, extension, ct);
        var oldPath = product.ImagePath;
        product.ImagePath = newPath;
        product.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            await media.DeleteAsync(newPath, CancellationToken.None); // don't leave an orphaned file
            throw;
        }

        if (oldPath is not null)
        {
            await media.DeleteAsync(oldPath, ct);
        }

        return await GetAsync(id, ct);
    }

    public async Task<AdminProductResponse> RemoveImageAsync(int id, CancellationToken ct)
    {
        var product = await Products.SingleOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException("Product not found.");
        if (product.ImagePath is { } path)
        {
            product.ImagePath = null;
            await db.SaveChangesAsync(ct);
            await media.DeleteAsync(path, ct);
        }

        return await GetAsync(id, ct);
    }

    private async Task ValidateReferencesAsync(IProductFields request, int? productId, CancellationToken ct)
    {
        var failures = new List<ValidationFailure>();

        if (!await db.Set<Category>().AnyAsync(c => c.Id == request.CategoryId, ct))
        {
            failures.Add(new ValidationFailure("categoryId", "Select a category."));
        }

        var sku = NullIfBlank(request.Sku);
        if (sku is not null && await Products.AnyAsync(p => p.Sku == sku && p.Id != productId, ct))
        {
            failures.Add(new ValidationFailure("sku", "Another product already uses this SKU."));
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }
    }

    private static ValidationException ImageError(string message) => new([new ValidationFailure("file", message)]);

    private static void Apply(IProductFields request, Product product, DateTime now)
    {
        product.CategoryId = request.CategoryId;
        product.Name = request.Name.Trim();
        product.Brand = NullIfBlank(request.Brand);
        product.Description = NullIfBlank(request.Description);
        product.UnitLabel = request.UnitLabel.Trim();
        product.Sku = NullIfBlank(request.Sku);
        product.Price = request.Price;
        product.Mrp = request.Mrp;
        product.LowStockThreshold = request.LowStockThreshold;
        product.MaxPerOrder = request.MaxPerOrder;
        product.IsActive = request.IsActive;
        product.IsFeatured = request.IsFeatured;
        product.UpdatedAtUtc = now;
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private AdminProductResponse ToResponse(Product p, string categoryName) => new(
        p.Id, p.CategoryId, categoryName, p.Name, p.Brand, p.Description, p.UnitLabel, p.Sku,
        p.Price, p.Mrp, ProductProjections.DiscountPercent(p.Price, p.Mrp),
        p.StockQuantity, p.LowStockThreshold, p.MaxPerOrder,
        ProductProjections.StatusOf(p.StockQuantity, p.LowStockThreshold),
        p.ImagePath is null ? null : media.PublicUrl(p.ImagePath),
        p.IsActive, p.IsFeatured, p.Version, p.UpdatedAtUtc);
}

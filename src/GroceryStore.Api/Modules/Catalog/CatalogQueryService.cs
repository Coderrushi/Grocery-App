using GroceryStore.Api.Common;
using GroceryStore.Api.Common.Errors;
using GroceryStore.Api.Common.Media;
using GroceryStore.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace GroceryStore.Api.Modules.Catalog;

/// <summary>Read-only catalogue queries for the storefront. Customers only ever see active products in active categories.</summary>
public sealed class CatalogQueryService(AppDbContext db, IMediaStorage media)
{
    public const int MaxPageSize = 48;
    private const int MaxSearchTerms = 5;

    private IQueryable<Product> VisibleProducts =>
        db.Set<Product>().Where(p => p.IsActive && p.Category.IsActive);

    public async Task<IReadOnlyList<CategoryResponse>> GetCategoriesAsync(CancellationToken ct) =>
        await db.Set<Category>()
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new CategoryResponse(
                c.Id, c.Name, c.Slug, c.Icon, c.Description,
                db.Set<Product>().Count(p => p.CategoryId == c.Id && p.IsActive)))
            .ToListAsync(ct);

    public async Task<PagedResult<ProductCardResponse>> SearchProductsAsync(ProductQuery query, CancellationToken ct)
    {
        var products = VisibleProducts;

        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            products = products.Where(p => p.Category.Slug == query.Category);
        }

        if (query.Featured)
        {
            products = products.Where(p => p.IsFeatured);
        }

        if (query.InStockOnly)
        {
            products = products.Where(p => p.StockQuantity > 0);
        }

        // Every word must match somewhere: "amul milk" finds "Amul Taaza Toned Milk".
        var terms = SearchTerms(query.Search);
        foreach (var term in terms)
        {
            products = products.Where(p =>
                p.Name.Contains(term) || (p.Brand != null && p.Brand.Contains(term)) || p.Category.Name.Contains(term));
        }

        var page = await Sort(products, query.Sort, terms)
            .Select(ProductProjections.ToRow)
            .ToPagedResultAsync(new PageRequest(query.Page, Math.Min(query.PageSize, MaxPageSize)), ct);

        return page.Map(row => ProductProjections.ToCard(row, media));
    }

    public async Task<ProductDetailResponse> GetProductAsync(int id, CancellationToken ct)
    {
        var detail = await VisibleProducts
            .Where(p => p.Id == id)
            .Select(p => new
            {
                Row = new ProductRow(p.Id, p.Name, p.Brand, p.UnitLabel, p.Price, p.Mrp, p.ImagePath, p.Category.Slug,
                    p.Category.Icon, p.StockQuantity, p.LowStockThreshold, p.MaxPerOrder),
                p.Description,
                CategoryName = p.Category.Name,
            })
            .SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException("This product is not available.");

        return new ProductDetailResponse(ProductProjections.ToCard(detail.Row, media), detail.Description, detail.CategoryName);
    }

    internal static string[] SearchTerms(string? search) =>
        string.IsNullOrWhiteSpace(search)
            ? []
            : [.. search.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(t => t.Length > 1 || char.IsLetterOrDigit(t[0]))
                .Take(MaxSearchTerms)];

    private static IOrderedQueryable<Product> Sort(IQueryable<Product> products, ProductSort sort, string[] terms)
    {
        // Out-of-stock items always sink to the end, whatever the chosen order.
        var ordered = products.OrderBy(p => p.StockQuantity > 0 ? 0 : 1);

        ordered = sort switch
        {
            ProductSort.PriceLowToHigh => ordered.ThenBy(p => p.Price),
            ProductSort.PriceHighToLow => ordered.ThenByDescending(p => p.Price),
            ProductSort.Name => ordered.ThenBy(p => p.Name),
            ProductSort.Newest => ordered.ThenByDescending(p => p.CreatedAtUtc),
            ProductSort.BiggestDiscount => ordered.ThenByDescending(p => (p.Mrp - p.Price) / p.Mrp),
            // Name starts with the word > name contains it > brand matches > only the aisle name matched.
            _ when terms.Length > 0 => ordered
                .ThenByDescending(p => p.Name.StartsWith(terms[0]))
                .ThenByDescending(p => p.Name.Contains(terms[0]))
                .ThenByDescending(p => p.Brand != null && p.Brand.Contains(terms[0]))
                .ThenByDescending(p => p.IsFeatured)
                .ThenBy(p => p.Name),
            _ => ordered.ThenByDescending(p => p.IsFeatured).ThenBy(p => p.Name),
        };

        return ordered.ThenBy(p => p.Id); // stable paging
    }
}

using System.Net;
using System.Net.Http.Json;
using GroceryStore.Api.Modules.Catalog;

namespace GroceryStore.Tests.Integration.Infrastructure;

/// <summary>Creates catalogue data through the admin API, with names unique to each test.</summary>
internal static class CatalogTestData
{
    public static string Unique(string prefix) => $"{prefix} {Guid.NewGuid().ToString("N")[..8]}";

    public static async Task<AdminCategoryResponse> CreateCategoryAsync(
        this HttpClient admin, string? name = null, bool isActive = true, CancellationToken ct = default)
    {
        var response = await admin.PostAsJsonAsync("/api/admin/categories",
            new SaveCategoryRequest(name ?? Unique("Category"), "🧪", null, 0, isActive), ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AdminCategoryResponse>(ct))!;
    }

    public static CreateProductRequest NewProduct(int categoryId, string? name = null) => new(
        CategoryId: categoryId,
        Name: name ?? Unique("Product"),
        Brand: "TestBrand",
        Description: "A product created by an integration test.",
        UnitLabel: "1 kg",
        Sku: null,
        Price: 90m,
        Mrp: 100m,
        LowStockThreshold: 5,
        MaxPerOrder: 10,
        IsActive: true,
        IsFeatured: false,
        InitialStock: 20);

    public static async Task<AdminProductResponse> CreateProductAsync(
        this HttpClient admin, CreateProductRequest request, CancellationToken ct = default)
    {
        var response = await admin.PostAsJsonAsync("/api/admin/products", request, ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AdminProductResponse>(ct))!;
    }

    public static UpdateProductRequest ToUpdate(this AdminProductResponse p) => new(
        p.CategoryId, p.Name, p.Brand, p.Description, p.UnitLabel, p.Sku, p.Price, p.Mrp,
        p.LowStockThreshold, p.MaxPerOrder, p.IsActive, p.IsFeatured, p.Version);
}

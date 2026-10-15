using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GroceryStore.Api.Modules.Catalog;
using GroceryStore.Tests.Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GroceryStore.Tests.Integration.Api;

public sealed class AdminCatalogTests(GroceryApiFactory factory)
{
    // A valid 1x1 pixel PNG.
    private static readonly byte[] TinyPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private async Task<JsonElement> ErrorsOf(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(_ct)).GetProperty("errors");

    [Fact]
    public async Task Catalog_admin_endpoints_require_the_admin_role()
    {
        var (customer, _) = await factory.SignedInCustomerAsync(_ct);

        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync("/api/admin/products", _ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await customer.PostAsJsonAsync("/api/admin/categories", new SaveCategoryRequest("Hack", null, null, 0, true), _ct)).StatusCode);
    }

    [Fact]
    public async Task Categories_have_unique_names_unique_slugs_and_cannot_be_deleted_while_in_use()
    {
        using var admin = await factory.SignedInAdminAsync(_ct);
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var first = await admin.CreateCategoryAsync($"Fruits & Veg {suffix}", ct: _ct);
        var similar = await admin.CreateCategoryAsync($"Fruits Veg {suffix}", ct: _ct);

        Assert.Equal($"fruits-veg-{suffix}", first.Slug);
        Assert.Equal($"fruits-veg-{suffix}-2", similar.Slug);

        var duplicate = await admin.PostAsJsonAsync("/api/admin/categories",
            new SaveCategoryRequest(first.Name.ToUpperInvariant(), null, null, 0, true), _ct);
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.True((await ErrorsOf(duplicate)).TryGetProperty("name", out _));

        await admin.CreateProductAsync(CatalogTestData.NewProduct(first.Id), _ct);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync($"/api/admin/categories/{first.Id}", _ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/admin/categories/{similar.Id}", _ct)).StatusCode);
    }

    [Fact]
    public async Task Product_rules_are_enforced_per_field()
    {
        using var admin = await factory.SignedInAdminAsync(_ct);
        var category = await admin.CreateCategoryAsync(ct: _ct);
        var sku = CatalogTestData.Unique("SKU").Replace(" ", "-", StringComparison.Ordinal);
        await admin.CreateProductAsync(CatalogTestData.NewProduct(category.Id) with { Sku = sku }, _ct);

        var invalid = await admin.PostAsJsonAsync("/api/admin/products",
            CatalogTestData.NewProduct(category.Id) with { Price = 10.005m, Mrp = 5m }, _ct);
        var badReferences = await admin.PostAsJsonAsync("/api/admin/products",
            CatalogTestData.NewProduct(999_999) with { Sku = sku }, _ct);

        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var errors = await ErrorsOf(invalid);
        Assert.True(errors.TryGetProperty("price", out _)); // 3 decimal places
        Assert.True(errors.TryGetProperty("mrp", out _)); // below price
        var referenceErrors = await ErrorsOf(badReferences);
        Assert.True(referenceErrors.TryGetProperty("categoryId", out _));
        Assert.True(referenceErrors.TryGetProperty("sku", out _));
    }

    [Fact]
    public async Task Concurrent_edits_are_detected_instead_of_silently_overwritten()
    {
        using var admin = await factory.SignedInAdminAsync(_ct);
        var category = await admin.CreateCategoryAsync(ct: _ct);
        var loaded = await admin.CreateProductAsync(CatalogTestData.NewProduct(category.Id), _ct);

        // Two staff members both opened the product at the same version.
        var first = await admin.PutAsJsonAsync($"/api/admin/products/{loaded.Id}", loaded.ToUpdate() with { Price = 85 }, _ct);
        var second = await admin.PutAsJsonAsync($"/api/admin/products/{loaded.Id}", loaded.ToUpdate() with { Price = 80 }, _ct);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var current = await admin.GetFromJsonAsync<AdminProductResponse>($"/api/admin/products/{loaded.Id}", _ct);
        Assert.Equal(85, current!.Price);
        Assert.Equal(loaded.Version + 1, current.Version);
    }

    [Fact]
    public async Task Stock_changes_only_through_recorded_adjustments_and_never_below_zero()
    {
        using var admin = await factory.SignedInAdminAsync(_ct);
        var category = await admin.CreateCategoryAsync(ct: _ct);
        var product = await admin.CreateProductAsync(CatalogTestData.NewProduct(category.Id) with { InitialStock = 10 }, _ct);
        var url = $"/api/admin/products/{product.Id}/stock-adjustments";

        var tooMuch = await admin.PostAsJsonAsync(url, new AdjustStockRequest(-11, InventoryReason.Correction, null), _ct);
        var wrongSign = await admin.PostAsJsonAsync(url, new AdjustStockRequest(3, InventoryReason.Damaged, null), _ct);
        var restock = await admin.PostAsJsonAsync(url, new AdjustStockRequest(5, InventoryReason.Restock, "Weekly delivery"), _ct);
        var damaged = await admin.PostAsJsonAsync(url, new AdjustStockRequest(-2, InventoryReason.Damaged, "Torn packets"), _ct);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, tooMuch.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, wrongSign.StatusCode);
        Assert.Equal(15, (await restock.Content.ReadFromJsonAsync<AdminProductResponse>(_ct))!.StockQuantity);
        Assert.Equal(13, (await damaged.Content.ReadFromJsonAsync<AdminProductResponse>(_ct))!.StockQuantity);

        var history = (await admin.GetFromJsonAsync<List<InventoryAdjustmentResponse>>(url, _ct))!;
        Assert.Equal(
            [(InventoryReason.Damaged, -2, 13), (InventoryReason.Restock, 5, 15), (InventoryReason.InitialStock, 10, 10)],
            history.Select(h => (h.Reason, h.QuantityChange, h.QuantityAfter)));
        Assert.All(history, h => Assert.Equal("Test Admin", h.CreatedBy));
    }

    [Fact]
    public async Task Concurrent_stock_removals_cannot_oversell()
    {
        using var admin = await factory.SignedInAdminAsync(_ct);
        var category = await admin.CreateCategoryAsync(ct: _ct);
        var product = await admin.CreateProductAsync(CatalogTestData.NewProduct(category.Id) with { InitialStock = 5 }, _ct);
        var url = $"/api/admin/products/{product.Id}/stock-adjustments";

        var responses = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ =>
            admin.PostAsJsonAsync(url, new AdjustStockRequest(-1, InventoryReason.Correction, null), _ct)));

        Assert.Equal(5, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(5, responses.Count(r => r.StatusCode == HttpStatusCode.UnprocessableEntity));
        Assert.Equal(0, (await admin.GetFromJsonAsync<AdminProductResponse>($"/api/admin/products/{product.Id}", _ct))!.StockQuantity);
    }

    [Fact]
    public async Task Database_rejects_negative_stock_even_from_raw_sql()
    {
        using var admin = await factory.SignedInAdminAsync(_ct);
        var category = await admin.CreateCategoryAsync(ct: _ct);
        var product = await admin.CreateProductAsync(CatalogTestData.NewProduct(category.Id), _ct);
        await using var db = factory.CreateDbContext();

        await Assert.ThrowsAnyAsync<Exception>(() =>
            db.Database.ExecuteSqlAsync($"UPDATE products SET StockQuantity = -1 WHERE Id = {product.Id}", _ct));
    }

    private static MultipartFormDataContent ImageForm(byte[] bytes, string fileName, string contentType = "image/png")
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return new MultipartFormDataContent { { file, "file", fileName } };
    }

    private static HttpRequestMessage UploadRequest(int productId, MultipartFormDataContent form, bool withXhrHeader = true)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/products/{productId}/image") { Content = form };
        if (withXhrHeader)
        {
            request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        }

        return request;
    }

    [Fact]
    public async Task Product_images_are_validated_by_content_stored_and_served_safely()
    {
        using var admin = await factory.SignedInAdminAsync(_ct);
        var category = await admin.CreateCategoryAsync(ct: _ct);
        var product = await admin.CreateProductAsync(CatalogTestData.NewProduct(category.Id), _ct);

        // A text file pretending to be a PNG is rejected.
        var fake = await admin.SendAsync(UploadRequest(product.Id, ImageForm("<script>alert(1)</script>"u8.ToArray(), "x.png")), _ct);
        Assert.Equal(HttpStatusCode.BadRequest, fake.StatusCode);
        Assert.True((await ErrorsOf(fake)).TryGetProperty("file", out _));

        // Without the CSRF header the upload is refused outright.
        var noHeader = await admin.SendAsync(UploadRequest(product.Id, ImageForm(TinyPng, "a.png"), withXhrHeader: false), _ct);
        Assert.Equal(HttpStatusCode.BadRequest, noHeader.StatusCode);

        // A real PNG (even with a misleading name) is stored under a random .png name.
        var upload = await admin.SendAsync(UploadRequest(product.Id, ImageForm(TinyPng, "photo.exe", "application/octet-stream")), _ct);
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var firstUrl = (await upload.Content.ReadFromJsonAsync<AdminProductResponse>(_ct))!.ImageUrl!;
        Assert.Matches("^/media/products/[0-9a-f]{32}\\.png$", firstUrl);

        using var anonymous = factory.CreateClient();
        var served = await anonymous.GetAsync(firstUrl, _ct);
        Assert.Equal(HttpStatusCode.OK, served.StatusCode);
        Assert.Equal("image/png", served.Content.Headers.ContentType?.MediaType);
        Assert.Equal("nosniff", served.Headers.GetValues("X-Content-Type-Options").Single());

        // Replacing the image deletes the old file.
        var replaced = await admin.SendAsync(UploadRequest(product.Id, ImageForm(TinyPng, "b.png")), _ct);
        var secondUrl = (await replaced.Content.ReadFromJsonAsync<AdminProductResponse>(_ct))!.ImageUrl!;
        Assert.NotEqual(firstUrl, secondUrl);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync(firstUrl, _ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync(secondUrl, _ct)).StatusCode);
    }
}

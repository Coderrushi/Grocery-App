using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using GroceryStore.Api.Modules.Catalog;
using GroceryStore.Tests.Integration.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;

namespace GroceryStore.Tests.Integration.Api;

/// <summary>Phase 9: the API serving the built web app, and production storage settings (ADR 0005).</summary>
public sealed class DeploymentTests(GroceryApiFactory factory) : IDisposable
{
    private const string ThemeScript = "document.documentElement.dataset.theme='light'";

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly string _site = Path.Combine(Path.GetTempPath(), "quickkirana-spa-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_site))
        {
            Directory.Delete(_site, recursive: true);
        }
    }

    /// <summary>A stand-in for web/dist: an index.html with one inline script and one hashed asset.</summary>
    private HttpClient SiteClient(params (string Key, string Value)[] settings)
    {
        Directory.CreateDirectory(Path.Combine(_site, "assets"));
        File.WriteAllText(Path.Combine(_site, "index.html"),
            $"<!doctype html><html><head><script>{ThemeScript}</script>" +
            "<script type=\"module\" src=\"/assets/index-AbC123.js\"></script></head><body><div id=\"root\"></div></body></html>");
        File.WriteAllText(Path.Combine(_site, "assets", "index-AbC123.js"), "console.log('app')");

        var host = factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("Spa:RootPath", _site);
            foreach (var (key, value) in settings)
            {
                b.UseSetting(key, value);
            }
        });
        return host.CreateClient();
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/account/orders/42")] // a React Router URL opened directly or refreshed
    public async Task Pages_get_the_web_app_with_a_strict_policy_that_allows_only_its_own_inline_script(string url)
    {
        using var client = SiteClient();

        var response = await client.GetAsync(url, _ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<div id=\"root\">", await response.Content.ReadAsStringAsync(_ct), StringComparison.Ordinal);
        Assert.True(response.Headers.CacheControl?.NoCache, "index.html must be revalidated so deploys show up at once");

        var hash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(ThemeScript)));
        var policy = string.Join(",", response.Headers.GetValues("Content-Security-Policy"));
        Assert.Contains($"script-src 'self' 'sha256-{hash}'", policy, StringComparison.Ordinal);
        Assert.Contains("frame-ancestors 'none'", policy, StringComparison.Ordinal);
        Assert.DoesNotContain("unsafe-inline", policy, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Hashed_assets_are_cached_for_a_year()
    {
        using var client = SiteClient();

        var response = await client.GetAsync("/assets/index-AbC123.js", _ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(TimeSpan.FromDays(365), response.Headers.CacheControl!.MaxAge);
    }

    [Theory]
    [InlineData("/api/no-such-endpoint")]
    [InlineData("/media/products/missing.webp")]
    [InlineData("/missing-file.png")]
    public async Task Unknown_api_media_and_file_urls_are_404_not_the_web_app(string url)
    {
        using var client = SiteClient();

        var response = await client.GetAsync(url, _ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotEqual("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task The_api_still_answers_next_to_the_web_app()
    {
        using var client = SiteClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/catalog/categories", _ct)).StatusCode);
    }

    [Fact]
    public async Task With_cloudinary_product_photos_come_from_the_cdn_and_pages_may_load_them()
    {
        using var admin = await factory.SignedInAdminAsync(_ct);
        var category = await admin.CreateCategoryAsync(ct: _ct);
        var product = await admin.CreateProductAsync(CatalogTestData.NewProduct(category.Id), _ct);
        await using (var db = factory.CreateDbContext())
        {
            (await db.Set<Product>().SingleAsync(p => p.Id == product.Id, _ct)).ImagePath = "products/abc123";
            await db.SaveChangesAsync(_ct);
        }

        using var client = SiteClient(
            ("Media:Provider", "Cloudinary"),
            ("Media:CloudinaryUrl", "cloudinary://123456:not-a-real-secret@demo-shop"),
            ("Database:MigrateOnStartup", "true")); // already migrated: must be a harmless no-op

        var card = await client.GetFromJsonAsync<ProductDetailResponse>($"/api/catalog/products/{product.Id}", _ct);
        Assert.Equal("https://res.cloudinary.com/demo-shop/image/upload/c_limit,w_800,f_auto,q_auto/products/abc123", card!.Product.ImageUrl);

        var page = await client.GetAsync("/", _ct);
        Assert.Contains("img-src 'self' data: https://res.cloudinary.com", string.Join(",", page.Headers.GetValues("Content-Security-Policy")), StringComparison.Ordinal);
    }

    [Fact]
    public void A_misconfigured_media_provider_stops_start_up()
    {
        using var host = factory.WithWebHostBuilder(b => b.UseSetting("Media:Provider", "Cloudinary"));

        var error = Assert.Throws<InvalidOperationException>(() => host.CreateClient());
        Assert.Contains("Media:CloudinaryUrl", error.Message, StringComparison.Ordinal);
    }
}

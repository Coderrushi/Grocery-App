using System.Net;
using System.Net.Http.Json;
using GroceryStore.Api.Common;
using GroceryStore.Api.Modules.Catalog;
using GroceryStore.Tests.Integration.Infrastructure;

namespace GroceryStore.Tests.Integration.Api;

public sealed class StorefrontCatalogTests(GroceryApiFactory factory)
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private async Task<PagedResult<ProductCardResponse>> BrowseAsync(string query)
    {
        using var anonymous = factory.CreateClient();
        return (await anonymous.GetFromJsonAsync<PagedResult<ProductCardResponse>>($"/api/catalog/products?{query}", _ct))!;
    }

    [Fact]
    public async Task Customers_never_see_inactive_products_or_products_in_hidden_categories()
    {
        using var admin = await factory.SignedInAdminAsync(_ct);
        var tag = CatalogTestData.Unique("vis").Replace(" ", "", StringComparison.Ordinal);
        var shown = await admin.CreateCategoryAsync(ct: _ct);
        var hidden = await admin.CreateCategoryAsync(isActive: false, ct: _ct);
        var visible = await admin.CreateProductAsync(CatalogTestData.NewProduct(shown.Id, $"{tag} visible"), _ct);
        await admin.CreateProductAsync(CatalogTestData.NewProduct(shown.Id, $"{tag} inactive") with { IsActive = false }, _ct);
        await admin.CreateProductAsync(CatalogTestData.NewProduct(hidden.Id, $"{tag} hidden-category"), _ct);

        var results = await BrowseAsync($"search={tag}");

        Assert.Equal(visible.Id, Assert.Single(results.Items).Id);
        using var anonymous = factory.CreateClient();
        var categories = await anonymous.GetFromJsonAsync<List<CategoryResponse>>("/api/catalog/categories", _ct);
        Assert.Contains(categories!, c => c.Id == shown.Id && c.ProductCount == 1);
        Assert.DoesNotContain(categories!, c => c.Id == hidden.Id);
    }

    [Fact]
    public async Task Search_requires_every_word_and_matches_name_brand_or_category_case_insensitively()
    {
        using var admin = await factory.SignedInAdminAsync(_ct);
        var tag = CatalogTestData.Unique("srch").Replace(" ", "", StringComparison.Ordinal);
        var category = await admin.CreateCategoryAsync(CatalogTestData.Unique("Dairy"), ct: _ct);
        var milk = await admin.CreateProductAsync(
            CatalogTestData.NewProduct(category.Id, $"{tag} Taaza Toned Milk") with { Brand = "Amul" }, _ct);
        await admin.CreateProductAsync(CatalogTestData.NewProduct(category.Id, $"{tag} Paneer") with { Brand = "Amul" }, _ct);

        Assert.Equal(milk.Id, Assert.Single((await BrowseAsync($"search={tag}+AMUL+milk")).Items).Id);
        Assert.Equal(2, (await BrowseAsync($"search={tag}+dairy")).TotalCount); // category name matches
        Assert.Empty((await BrowseAsync($"search={tag}+butter")).Items);
    }

    [Fact]
    public async Task Search_treats_sql_wildcards_as_plain_text()
    {
        using var admin = await factory.SignedInAdminAsync(_ct);
        var tag = CatalogTestData.Unique("wild").Replace(" ", "", StringComparison.Ordinal);
        var category = await admin.CreateCategoryAsync(ct: _ct);
        var juice = await admin.CreateProductAsync(CatalogTestData.NewProduct(category.Id, $"{tag} 100% Orange Juice"), _ct);
        await admin.CreateProductAsync(CatalogTestData.NewProduct(category.Id, $"{tag} 100 Bananas"), _ct);
        await admin.CreateProductAsync(CatalogTestData.NewProduct(category.Id, $"{tag} Rice_Flour"), _ct);

        Assert.Equal(juice.Id, Assert.Single((await BrowseAsync($"search={tag}+100%25")).Items).Id);
        Assert.Single((await BrowseAsync($"search={tag}+e_f")).Items, p => p.Name.EndsWith("Rice_Flour", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Sorting_by_price_puts_out_of_stock_items_last()
    {
        using var admin = await factory.SignedInAdminAsync(_ct);
        var category = await admin.CreateCategoryAsync(ct: _ct);
        await admin.CreateProductAsync(CatalogTestData.NewProduct(category.Id, "Cheap but sold out") with { Price = 10, Mrp = 10, InitialStock = 0 }, _ct);
        await admin.CreateProductAsync(CatalogTestData.NewProduct(category.Id, "Expensive") with { Price = 300, Mrp = 300 }, _ct);
        await admin.CreateProductAsync(CatalogTestData.NewProduct(category.Id, "Middle") with { Price = 50, Mrp = 60 }, _ct);

        var results = await BrowseAsync($"category={category.Slug}&sort=PriceLowToHigh");

        Assert.Equal(["Middle", "Expensive", "Cheap but sold out"], results.Items.Select(p => p.Name));
        Assert.Equal(2, (await BrowseAsync($"category={category.Slug}&inStockOnly=true")).TotalCount);
    }

    [Fact]
    public async Task Product_cards_show_discount_and_stock_signals_without_revealing_exact_stock()
    {
        using var admin = await factory.SignedInAdminAsync(_ct);
        var category = await admin.CreateCategoryAsync(ct: _ct);
        var plenty = await admin.CreateProductAsync(
            CatalogTestData.NewProduct(category.Id, "Plenty") with { Price = 52, Mrp = 56, InitialStock = 40, MaxPerOrder = 6 }, _ct);
        var few = await admin.CreateProductAsync(
            CatalogTestData.NewProduct(category.Id, "Few") with { InitialStock = 3, LowStockThreshold = 5 }, _ct);

        var cards = (await BrowseAsync($"category={category.Slug}")).Items.ToDictionary(p => p.Id);

        Assert.Equal(7, cards[plenty.Id].DiscountPercent); // 4/56 = 7.14% -> 7 (never rounded up)
        Assert.Equal(StockStatus.InStock, cards[plenty.Id].StockStatus);
        Assert.Null(cards[plenty.Id].OnlyLeft);
        Assert.Equal(6, cards[plenty.Id].MaxQuantity); // capped by MaxPerOrder, not stock
        Assert.Equal(StockStatus.LowStock, cards[few.Id].StockStatus);
        Assert.Equal(3, cards[few.Id].OnlyLeft);
        Assert.Equal(3, cards[few.Id].MaxQuantity); // capped by stock
    }

    [Fact]
    public async Task Product_detail_is_404_for_inactive_products()
    {
        using var admin = await factory.SignedInAdminAsync(_ct);
        var category = await admin.CreateCategoryAsync(ct: _ct);
        var active = await admin.CreateProductAsync(CatalogTestData.NewProduct(category.Id), _ct);
        var inactive = await admin.CreateProductAsync(CatalogTestData.NewProduct(category.Id) with { IsActive = false }, _ct);
        using var anonymous = factory.CreateClient();

        var detail = await anonymous.GetFromJsonAsync<ProductDetailResponse>($"/api/catalog/products/{active.Id}", _ct);
        Assert.Equal(category.Name, detail!.CategoryName);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/api/catalog/products/{inactive.Id}", _ct)).StatusCode);
    }
}

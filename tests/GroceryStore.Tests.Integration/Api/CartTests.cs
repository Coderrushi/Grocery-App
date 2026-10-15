using System.Net;
using System.Net.Http.Json;
using GroceryStore.Api.Modules.Cart;
using GroceryStore.Api.Modules.Catalog;
using GroceryStore.Tests.Integration.Infrastructure;

namespace GroceryStore.Tests.Integration.Api;

/// <summary>Uses the default delivery rules from appsettings.json: ₹25 fee, free from ₹299, ₹99 minimum.</summary>
public sealed class CartTests(GroceryApiFactory factory)
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private async Task<(HttpClient Admin, int CategoryId)> AdminWithCategoryAsync()
    {
        var admin = await factory.SignedInAdminAsync(_ct);
        var category = await admin.CreateCategoryAsync(ct: _ct);
        return (admin, category.Id);
    }

    private async Task<HttpResponseMessage> SetAsync(HttpClient client, int productId, int quantity) =>
        await client.PutAsJsonAsync($"/api/cart/items/{productId}", new SetCartQuantityRequest(quantity), _ct);

    private async Task<CartResponse> SetOkAsync(HttpClient client, int productId, int quantity)
    {
        var response = await SetAsync(client, productId, quantity);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CartResponse>(_ct))!;
    }

    private async Task<CartResponse> GetCartAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<CartResponse>("/api/cart", _ct))!;

    [Fact]
    public async Task Saved_cart_requires_sign_in_but_guests_can_price_a_cart()
    {
        var (admin, categoryId) = await AdminWithCategoryAsync();
        var product = await admin.CreateProductAsync(CatalogTestData.NewProduct(categoryId), _ct);
        using var anonymous = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/cart", _ct)).StatusCode);

        var preview = await anonymous.PostAsJsonAsync("/api/cart/preview",
            new GuestCartRequest([new(product.Id, 1), new(product.Id, 2)]), _ct); // duplicates are combined
        var cart = (await preview.Content.ReadFromJsonAsync<CartResponse>(_ct))!;
        Assert.Equal(3, Assert.Single(cart.Lines).Quantity);
        Assert.Equal(270m, cart.Totals.Subtotal);
    }

    [Fact]
    public async Task Adding_changing_and_removing_items_keeps_totals_right()
    {
        var (admin, categoryId) = await AdminWithCategoryAsync();
        var paneer = await admin.CreateProductAsync(CatalogTestData.NewProduct(categoryId, "Paneer") with { Price = 90, Mrp = 100 }, _ct);
        var milk = await admin.CreateProductAsync(CatalogTestData.NewProduct(categoryId, "Milk") with { Price = 28, Mrp = 28 }, _ct);
        var (client, _) = await factory.SignedInCustomerAsync(_ct);

        await SetOkAsync(client, paneer.Id, 2);
        var cart = await SetOkAsync(client, milk.Id, 1);

        Assert.Equal(["Paneer", "Milk"], cart.Lines.Select(l => l.Name)); // in the order added
        Assert.Equal(208m, cart.Totals.Subtotal);
        Assert.Equal(20m, cart.Totals.Savings);
        Assert.Equal(25m, cart.Totals.DeliveryFee);
        Assert.Equal(233m, cart.Totals.Total);
        Assert.Equal(91m, cart.Totals.AmountToFreeDelivery);
        Assert.True(cart.CanCheckout);

        cart = await SetOkAsync(client, paneer.Id, 4); // 360 + 28: free delivery
        Assert.Equal(0m, cart.Totals.DeliveryFee);
        Assert.Equal(388m, cart.Totals.Total);

        cart = await SetOkAsync(client, paneer.Id, 0); // 0 removes the line
        Assert.Equal("Milk", Assert.Single(cart.Lines).Name);
        Assert.False(cart.Totals.MeetsMinimumOrder); // ₹28 is below the ₹99 minimum
        Assert.False(cart.CanCheckout);

        var cleared = await client.DeleteAsync("/api/cart", _ct);
        Assert.Empty((await cleared.Content.ReadFromJsonAsync<CartResponse>(_ct))!.Lines);
    }

    [Fact]
    public async Task Quantities_are_limited_by_stock_and_by_the_per_order_limit()
    {
        var (admin, categoryId) = await AdminWithCategoryAsync();
        var scarce = await admin.CreateProductAsync(CatalogTestData.NewProduct(categoryId) with { InitialStock = 3 }, _ct);
        var limited = await admin.CreateProductAsync(CatalogTestData.NewProduct(categoryId) with { MaxPerOrder = 2 }, _ct);
        var soldOut = await admin.CreateProductAsync(CatalogTestData.NewProduct(categoryId) with { InitialStock = 0 }, _ct);
        var hidden = await admin.CreateProductAsync(CatalogTestData.NewProduct(categoryId) with { IsActive = false }, _ct);
        var (client, _) = await factory.SignedInCustomerAsync(_ct);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await SetAsync(client, scarce.Id, 4)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SetAsync(client, scarce.Id, 3)).StatusCode);
        var overLimit = await SetAsync(client, limited.Id, 3);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, overLimit.StatusCode);
        Assert.Contains("at most 2", await overLimit.Content.ReadAsStringAsync(_ct), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await SetAsync(client, soldOut.Id, 1)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SetAsync(client, hidden.Id, 1)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await SetAsync(client, scarce.Id, 101)).StatusCode);
    }

    [Fact]
    public async Task Catalogue_changes_after_adding_are_flagged_and_block_checkout()
    {
        var (admin, categoryId) = await AdminWithCategoryAsync();
        var shrinking = await admin.CreateProductAsync(CatalogTestData.NewProduct(categoryId, "Shrinking") with { Price = 100, Mrp = 100 }, _ct);
        var vanishing = await admin.CreateProductAsync(CatalogTestData.NewProduct(categoryId, "Vanishing") with { Price = 100, Mrp = 100 }, _ct);
        var fine = await admin.CreateProductAsync(CatalogTestData.NewProduct(categoryId, "Fine") with { Price = 100, Mrp = 100 }, _ct);
        var (client, _) = await factory.SignedInCustomerAsync(_ct);
        await SetOkAsync(client, shrinking.Id, 3);
        await SetOkAsync(client, vanishing.Id, 1);
        await SetOkAsync(client, fine.Id, 1);

        // The shopkeeper finds 18 damaged (20 -> 2) and hides another product.
        await admin.PostAsJsonAsync($"/api/admin/products/{shrinking.Id}/stock-adjustments",
            new AdjustStockRequest(-18, InventoryReason.Damaged, null), _ct);
        await admin.PutAsJsonAsync($"/api/admin/products/{vanishing.Id}", vanishing.ToUpdate() with { IsActive = false }, _ct);

        var cart = await GetCartAsync(client);
        var lines = cart.Lines.ToDictionary(l => l.Name);
        Assert.Equal(CartLineIssue.ExceedsAvailable, lines["Shrinking"].Issue);
        Assert.Equal(2, lines["Shrinking"].MaxQuantity);
        Assert.Equal(CartLineIssue.Unavailable, lines["Vanishing"].Issue);
        Assert.Null(lines["Fine"].Issue);
        Assert.Equal(100m, cart.Totals.Subtotal); // only the line that can be bought
        Assert.True(cart.HasIssues);
        Assert.False(cart.CanCheckout);

        // Fixing the lines makes the cart valid again.
        await SetOkAsync(client, shrinking.Id, 2);
        cart = (await (await client.DeleteAsync($"/api/cart/items/{vanishing.Id}", _ct)).Content.ReadFromJsonAsync<CartResponse>(_ct))!;
        Assert.False(cart.HasIssues);
        Assert.True(cart.CanCheckout);
        Assert.Equal(300m, cart.Totals.Subtotal);
    }

    [Fact]
    public async Task Rapid_concurrent_quantity_changes_never_create_duplicate_lines()
    {
        var (admin, categoryId) = await AdminWithCategoryAsync();
        var product = await admin.CreateProductAsync(CatalogTestData.NewProduct(categoryId), _ct);
        var (client, _) = await factory.SignedInCustomerAsync(_ct);

        var responses = await Task.WhenAll(Enumerable.Range(1, 10).Select(q => SetAsync(client, product.Id, q)));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        var line = Assert.Single((await GetCartAsync(client)).Lines);
        Assert.InRange(line.Quantity, 1, 10);
    }

    [Fact]
    public async Task Guest_cart_merges_into_the_account_skipping_and_capping_what_cannot_be_bought()
    {
        var (admin, categoryId) = await AdminWithCategoryAsync();
        var a = await admin.CreateProductAsync(CatalogTestData.NewProduct(categoryId, "A"), _ct);
        var b = await admin.CreateProductAsync(CatalogTestData.NewProduct(categoryId, "B") with { MaxPerOrder = 10 }, _ct);
        var hidden = await admin.CreateProductAsync(CatalogTestData.NewProduct(categoryId, "Hidden") with { IsActive = false }, _ct);
        var (client, _) = await factory.SignedInCustomerAsync(_ct);
        await SetOkAsync(client, a.Id, 1); // already in the saved cart

        var response = await client.PostAsJsonAsync("/api/cart/merge", new GuestCartRequest(
        [
            new(a.Id, 2),
            new(b.Id, 50),
            new(hidden.Id, 1),
            new(999_999, 1),
        ]), _ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cart = (await response.Content.ReadFromJsonAsync<CartResponse>(_ct))!;
        Assert.Equal(
            [("A", 3), ("B", 10)],
            cart.Lines.Select(l => (l.Name, l.Quantity)));
    }

    [Fact]
    public async Task Each_customer_has_their_own_cart()
    {
        var (admin, categoryId) = await AdminWithCategoryAsync();
        var product = await admin.CreateProductAsync(CatalogTestData.NewProduct(categoryId), _ct);
        var (first, _) = await factory.SignedInCustomerAsync(_ct);
        var (second, _) = await factory.SignedInCustomerAsync(_ct);

        await SetOkAsync(first, product.Id, 2);

        Assert.Empty((await GetCartAsync(second)).Lines);
    }

    [Fact]
    public async Task Guest_cart_requests_are_validated()
    {
        using var anonymous = factory.CreateClient();
        var tooMany = new GuestCartRequest([.. Enumerable.Range(1, CartService.MaxLines + 1).Select(i => new CartLineRequest(i, 1))]);

        Assert.Equal(HttpStatusCode.BadRequest, (await anonymous.PostAsJsonAsync("/api/cart/preview", tooMany, _ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await anonymous.PostAsJsonAsync("/api/cart/preview", new GuestCartRequest([new(1, 0)]), _ct)).StatusCode);
    }
}

using System.Net;
using System.Net.Http.Json;
using GroceryStore.Api.Common;
using GroceryStore.Api.Modules.Cart;
using GroceryStore.Api.Modules.Catalog;
using GroceryStore.Api.Modules.Customers;
using GroceryStore.Api.Modules.Orders;
using GroceryStore.Api.Modules.Wallet;
using GroceryStore.Tests.Integration.Infrastructure;
using Microsoft.AspNetCore.Hosting;

namespace GroceryStore.Tests.Integration.Api;

public sealed class OrderTests(GroceryApiFactory factory)
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private sealed record Shopper(HttpClient Client, int AddressId, int ProductId, decimal ExpectedTotal);

    private static SaveAddressRequest Address(string pincode = "411038") =>
        new(AddressLabel.Home, "Ravi Kumar", "9876543210", "Flat 4B, Shanti Apartments", "MG Road", null,
            "Pune", "Maharashtra", pincode, true);

    /// <summary>A signed-in customer with wallet money, a saved address and a product in the cart.</summary>
    private async Task<Shopper> ShopperAsync(
        int productId, int quantity, decimal topUp = 1000m, string pincode = "411038", HttpClient? client = null)
    {
        client ??= (await factory.SignedInCustomerAsync(_ct)).Client;
        if (topUp > 0)
        {
            await client.PostAsJsonAsync("/api/wallet/top-ups", new TopUpRequest(topUp, Guid.NewGuid().ToString()), _ct);
        }

        var address = (await (await client.PostAsJsonAsync("/api/account/addresses", Address(pincode), _ct))
            .Content.ReadFromJsonAsync<AddressResponse>(_ct))!;
        var cart = (await (await client.PutAsJsonAsync($"/api/cart/items/{productId}", new SetCartQuantityRequest(quantity), _ct))
            .Content.ReadFromJsonAsync<CartResponse>(_ct))!;
        return new Shopper(client, address.Id, productId, cart.Totals.Total);
    }

    private async Task<AdminProductResponse> ProductAsync(HttpClient admin, decimal price = 100m, int stock = 20)
    {
        var category = await admin.CreateCategoryAsync(ct: _ct);
        return await admin.CreateProductAsync(
            CatalogTestData.NewProduct(category.Id) with { Price = price, Mrp = price + 10, InitialStock = stock }, _ct);
    }

    private Task<HttpResponseMessage> PlaceAsync(Shopper s, string? key = null, decimal? expectedTotal = null) =>
        s.Client.PostAsJsonAsync("/api/orders",
            new PlaceOrderRequest(s.AddressId, "Ring the bell twice", expectedTotal ?? s.ExpectedTotal, key ?? Guid.NewGuid().ToString()), _ct);

    private async Task<OrderDetailResponse> PlaceOkAsync(Shopper s)
    {
        var response = await PlaceAsync(s);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<OrderDetailResponse>(_ct))!;
    }

    private async Task<int> StockAsync(HttpClient admin, int productId) =>
        (await admin.GetFromJsonAsync<AdminProductResponse>($"/api/admin/products/{productId}", _ct))!.StockQuantity;

    private async Task<decimal> BalanceAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<WalletResponse>("/api/wallet", _ct))!.Balance;

    private async Task<CartResponse> CartAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<CartResponse>("/api/cart", _ct))!;

    // ---------- Placing orders ----------

    [Fact]
    public async Task Placing_an_order_charges_the_wallet_reserves_stock_and_empties_the_cart()
    {
        using var admin = await factory.SignedInAdminAsync(_ct);
        var product = await ProductAsync(admin, price: 100m, stock: 20);
        var shopper = await ShopperAsync(product.Id, quantity: 2, topUp: 1000m);

        var order = await PlaceOkAsync(shopper);

        Assert.Equal(OrderStatus.Placed, order.Status);
        Assert.Matches("^QK-\\d{6}$", order.OrderNumber);
        Assert.Equal(200m, order.Subtotal);
        Assert.Equal(25m, order.DeliveryFee); // below the ₹299 free-delivery threshold
        Assert.Equal(225m, order.Total);
        Assert.Equal(20m, order.Savings);
        Assert.Equal("Ring the bell twice", order.DeliveryInstructions);
        Assert.Equal("411038", order.DeliveryAddress.Pincode);
        Assert.True(order.CanCancel);
        Assert.Equal(OrderStatus.Placed, Assert.Single(order.Timeline).ToStatus);

        Assert.Equal(775m, await BalanceAsync(shopper.Client));
        Assert.Equal(18, await StockAsync(admin, product.Id));
        Assert.Empty((await CartAsync(shopper.Client)).Lines);

        var payment = (await shopper.Client.GetFromJsonAsync<PagedResult<WalletTransactionResponse>>("/api/wallet/transactions", _ct))!.Items[0];
        Assert.Equal((WalletTransactionType.OrderPayment, -225m), (payment.Type, payment.Amount));
        var stockHistory = await admin.GetFromJsonAsync<List<InventoryAdjustmentResponse>>($"/api/admin/products/{product.Id}/stock-adjustments", _ct);
        Assert.Equal((InventoryReason.Sale, -2, order.OrderNumber), (stockHistory![0].Reason, stockHistory[0].QuantityChange, stockHistory[0].Note));
    }

    [Fact]
    public async Task Orders_keep_the_prices_and_address_from_the_moment_they_were_placed()
    {
        using var admin = await factory.SignedInAdminAsync(_ct);
        var product = await ProductAsync(admin, price: 100m);
        var shopper = await ShopperAsync(product.Id, quantity: 1);
        var order = await PlaceOkAsync(shopper);

        var current = await admin.GetFromJsonAsync<AdminProductResponse>($"/api/admin/products/{product.Id}", _ct);
        await admin.PutAsJsonAsync($"/api/admin/products/{product.Id}", current!.ToUpdate() with { Price = 150, Mrp = 160, Name = "Renamed" }, _ct);
        await shopper.Client.PutAsJsonAsync($"/api/account/addresses/{shopper.AddressId}", Address("110001") with { City = "Delhi", State = "Delhi" }, _ct);

        var reloaded = await shopper.Client.GetFromJsonAsync<OrderDetailResponse>($"/api/orders/{order.Id}", _ct);

        Assert.Equal(100m, reloaded!.Items[0].UnitPrice);
        Assert.Equal(product.Name, reloaded.Items[0].Name);
        Assert.Equal(("Pune", "411038"), (reloaded.DeliveryAddress.City, reloaded.DeliveryAddress.Pincode));
    }

    [Fact]
    public async Task A_changed_price_is_refused_and_nothing_changes()
    {
        using var admin = await factory.SignedInAdminAsync(_ct);
        var product = await ProductAsync(admin, price: 100m, stock: 10);
        var shopper = await ShopperAsync(product.Id, quantity: 1);

        // The shopkeeper raises the price while the customer is on the checkout page.
        var current = await admin.GetFromJsonAsync<AdminProductResponse>($"/api/admin/products/{product.Id}", _ct);
        await admin.PutAsJsonAsync($"/api/admin/products/{product.Id}", current!.ToUpdate() with { Price = 110 }, _ct);

        var response = await PlaceAsync(shopper);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("Prices have changed", await response.Content.ReadAsStringAsync(_ct), StringComparison.Ordinal);
        Assert.Equal(1000m, await BalanceAsync(shopper.Client));
        Assert.Equal(10, await StockAsync(admin, product.Id));
        Assert.Single((await CartAsync(shopper.Client)).Lines);
    }

    [Fact]
    public async Task Insufficient_wallet_balance_rolls_everything_back()
    {
        using var admin = await factory.SignedInAdminAsync(_ct);
        var product = await ProductAsync(admin, price: 100m, stock: 10);
        var shopper = await ShopperAsync(product.Id, quantity: 2, topUp: 50m);

        var response = await PlaceAsync(shopper);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("Insufficient wallet balance", await response.Content.ReadAsStringAsync(_ct), StringComparison.Ordinal);
        Assert.Equal(10, await StockAsync(admin, product.Id)); // the stock reduction was rolled back too
        Assert.Single((await CartAsync(shopper.Client)).Lines);
        Assert.Empty((await shopper.Client.GetFromJsonAsync<PagedResult<OrderSummaryResponse>>("/api/orders", _ct))!.Items);
    }

    [Fact]
    public async Task Checkout_rejects_an_empty_cart_and_someone_elses_address()
    {
        using var admin = await factory.SignedInAdminAsync(_ct);
        var product = await ProductAsync(admin);
        var shopper = await ShopperAsync(product.Id, quantity: 1);
        var other = await ShopperAsync(product.Id, quantity: 1);

        var foreignAddress = await PlaceAsync(shopper with { AddressId = other.AddressId });
        await shopper.Client.DeleteAsync("/api/cart", _ct);
        var emptyCart = await PlaceAsync(shopper);

        Assert.Equal(HttpStatusCode.NotFound, foreignAddress.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, emptyCart.StatusCode);
    }

    [Fact]
    public async Task Addresses_outside_the_delivery_area_are_refused()
    {
        await using var localShop = factory.WithWebHostBuilder(b => b.UseSetting("DeliveryArea:ServiceablePincodes:0", "411038"));
        using var admin = localShop.CreateClient();
        await admin.PostAsJsonAsync("/api/auth/login",
            new GroceryStore.Api.Modules.Identity.LoginRequest(GroceryApiFactory.AdminEmail, GroceryApiFactory.AdminPassword), _ct);
        var product = await ProductAsync(admin);
        using var client = localShop.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", TestUsers.NewCustomer(), _ct);
        var farAway = await ShopperAsync(product.Id, 1, pincode: "560001", client: client);

        var summary = await client.GetFromJsonAsync<CheckoutSummaryResponse>("/api/checkout", _ct);
        var response = await PlaceAsync(farAway);

        Assert.False(Assert.Single(summary!.Addresses).IsServiceable);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("560001", await response.Content.ReadAsStringAsync(_ct), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Tapping_place_order_repeatedly_creates_one_order_and_one_payment()
    {
        using var admin = await factory.SignedInAdminAsync(_ct);
        var product = await ProductAsync(admin, price: 100m, stock: 10);
        var shopper = await ShopperAsync(product.Id, quantity: 1);
        var key = Guid.NewGuid().ToString();

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => PlaceAsync(shopper, key)));

        var ids = new HashSet<int>();
        foreach (var r in responses)
        {
            Assert.Equal(HttpStatusCode.Created, r.StatusCode);
            ids.Add((await r.Content.ReadFromJsonAsync<OrderDetailResponse>(_ct))!.Id);
        }

        Assert.Single(ids);
        Assert.Equal(875m, await BalanceAsync(shopper.Client)); // 1000 - (100 + 25 delivery), once
        Assert.Equal(9, await StockAsync(admin, product.Id));
    }

    [Fact]
    public async Task Customers_racing_for_the_last_units_cannot_oversell()
    {
        using var admin = await factory.SignedInAdminAsync(_ct);
        var product = await ProductAsync(admin, price: 100m, stock: 3);
        var shoppers = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => ShopperAsync(product.Id, quantity: 2)));

        var responses = await Task.WhenAll(shoppers.Select(s => PlaceAsync(s)));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.Created),
            r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
        Assert.Equal(1, await StockAsync(admin, product.Id));
        // Only the winner paid.
        Assert.Equal(1, (await Task.WhenAll(shoppers.Select(s => BalanceAsync(s.Client)))).Count(b => b < 1000m));
    }

    // ---------- Cancelling ----------

    [Fact]
    public async Task Customer_cancellation_refunds_the_wallet_and_restocks_once()
    {
        using var admin = await factory.SignedInAdminAsync(_ct);
        var product = await ProductAsync(admin, price: 100m, stock: 10);
        var shopper = await ShopperAsync(product.Id, quantity: 3);
        var order = await PlaceOkAsync(shopper);

        var cancel = await shopper.Client.PostAsJsonAsync($"/api/orders/{order.Id}/cancel", new CancelOrderRequest("Ordered by mistake"), _ct);
        var again = await shopper.Client.PostAsJsonAsync($"/api/orders/{order.Id}/cancel", new CancelOrderRequest(null), _ct);

        var cancelled = (await cancel.Content.ReadFromJsonAsync<OrderDetailResponse>(_ct))!;
        Assert.Equal(OrderStatus.Cancelled, cancelled.Status);
        Assert.Equal(PaymentStatus.Refunded, cancelled.PaymentStatus);
        Assert.Equal("Ordered by mistake", cancelled.CancellationReason);
        Assert.False(cancelled.CanCancel);
        Assert.Equal([OrderStatus.Placed, OrderStatus.Cancelled], cancelled.Timeline.Select(t => t.ToStatus));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, again.StatusCode);

        Assert.Equal(1000m, await BalanceAsync(shopper.Client));
        Assert.Equal(10, await StockAsync(admin, product.Id));
    }

    [Fact]
    public async Task Customers_cannot_cancel_once_the_store_has_confirmed()
    {
        using var admin = await factory.SignedInAdminAsync(_ct);
        var product = await ProductAsync(admin);
        var shopper = await ShopperAsync(product.Id, quantity: 1);
        var order = await PlaceOkAsync(shopper);
        await admin.PostAsJsonAsync($"/api/admin/orders/{order.Id}/status", new ChangeOrderStatusRequest(OrderStatus.Confirmed, null, 0), _ct);

        var cancel = await shopper.Client.PostAsJsonAsync($"/api/orders/{order.Id}/cancel", new CancelOrderRequest(null), _ct);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, cancel.StatusCode);
        Assert.Contains("call the store", await cancel.Content.ReadAsStringAsync(_ct), StringComparison.Ordinal);
    }

    // ---------- Admin workflow ----------

    private Task<HttpResponseMessage> MoveAsync(HttpClient admin, int orderId, OrderStatus to, int version, string? note = null) =>
        admin.PostAsJsonAsync($"/api/admin/orders/{orderId}/status", new ChangeOrderStatusRequest(to, note, version), _ct);

    [Fact]
    public async Task Admin_moves_an_order_through_the_workflow_to_delivery()
    {
        using var admin = await factory.SignedInAdminAsync(_ct);
        var product = await ProductAsync(admin);
        var shopper = await ShopperAsync(product.Id, quantity: 1);
        var order = await PlaceOkAsync(shopper);

        var skip = await MoveAsync(admin, order.Id, OrderStatus.Delivered, 0);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, skip.StatusCode);

        var version = 0;
        foreach (var next in new[] { OrderStatus.Confirmed, OrderStatus.Packed, OrderStatus.OutForDelivery, OrderStatus.Delivered })
        {
            var response = await MoveAsync(admin, order.Id, next, version);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var detail = (await response.Content.ReadFromJsonAsync<AdminOrderDetailResponse>(_ct))!;
            Assert.Equal(next, detail.Order.Status);
            version = detail.Version;
        }

        var delivered = (await admin.GetFromJsonAsync<AdminOrderDetailResponse>($"/api/admin/orders/{order.Id}", _ct))!;
        Assert.Empty(delivered.AllowedNextStatuses);
        Assert.Equal(5, delivered.Order.Timeline.Count);
        Assert.Equal("Test Admin", delivered.Order.Timeline[^1].ChangedBy);

        var customerView = await shopper.Client.GetFromJsonAsync<OrderDetailResponse>($"/api/orders/{order.Id}", _ct);
        Assert.Equal("Store", customerView!.Timeline[^1].ChangedBy); // customers don't see staff names
    }

    [Fact]
    public async Task Stale_admin_screens_get_409_instead_of_overwriting()
    {
        using var admin = await factory.SignedInAdminAsync(_ct);
        var product = await ProductAsync(admin);
        var shopper = await ShopperAsync(product.Id, quantity: 1);
        var order = await PlaceOkAsync(shopper);

        Assert.Equal(HttpStatusCode.OK, (await MoveAsync(admin, order.Id, OrderStatus.Confirmed, 0)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await MoveAsync(admin, order.Id, OrderStatus.Cancelled, 0, "Out of stock")).StatusCode);
    }

    [Fact]
    public async Task Admin_cancellation_needs_a_reason_and_refunds_even_after_packing()
    {
        using var admin = await factory.SignedInAdminAsync(_ct);
        var product = await ProductAsync(admin, price: 100m, stock: 5);
        var shopper = await ShopperAsync(product.Id, quantity: 2);
        var order = await PlaceOkAsync(shopper);
        await MoveAsync(admin, order.Id, OrderStatus.Confirmed, 0);
        await MoveAsync(admin, order.Id, OrderStatus.Packed, 1);

        var noReason = await MoveAsync(admin, order.Id, OrderStatus.Cancelled, 2);
        var cancel = await MoveAsync(admin, order.Id, OrderStatus.Cancelled, 2, "Customer not reachable");

        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        Assert.Equal(1000m, await BalanceAsync(shopper.Client));
        Assert.Equal(5, await StockAsync(admin, product.Id));
        var refund = (await shopper.Client.GetFromJsonAsync<PagedResult<WalletTransactionResponse>>("/api/wallet/transactions", _ct))!.Items[0];
        Assert.Equal(WalletTransactionType.OrderRefund, refund.Type);
    }

    [Fact]
    public async Task Customer_cancel_and_admin_confirm_at_the_same_moment_cannot_both_win()
    {
        using var admin = await factory.SignedInAdminAsync(_ct);
        var product = await ProductAsync(admin, price: 100m, stock: 10);
        var shopper = await ShopperAsync(product.Id, quantity: 1);
        var order = await PlaceOkAsync(shopper);

        var results = await Task.WhenAll(
            shopper.Client.PostAsJsonAsync($"/api/orders/{order.Id}/cancel", new CancelOrderRequest(null), _ct),
            MoveAsync(admin, order.Id, OrderStatus.Confirmed, 0));

        Assert.Equal(1, results.Count(r => r.StatusCode == HttpStatusCode.OK));
        var final = (await admin.GetFromJsonAsync<AdminOrderDetailResponse>($"/api/admin/orders/{order.Id}", _ct))!;
        if (final.Order.Status == OrderStatus.Cancelled)
        {
            Assert.Equal(1000m, await BalanceAsync(shopper.Client));
            Assert.Equal(10, await StockAsync(admin, product.Id));
        }
        else
        {
            Assert.Equal(OrderStatus.Confirmed, final.Order.Status);
            Assert.Equal(875m, await BalanceAsync(shopper.Client));
            Assert.Equal(9, await StockAsync(admin, product.Id));
        }
    }

    // ---------- Access and lists ----------

    [Fact]
    public async Task Orders_are_private_and_admin_lists_are_admin_only()
    {
        using var admin = await factory.SignedInAdminAsync(_ct);
        var product = await ProductAsync(admin);
        var owner = await ShopperAsync(product.Id, quantity: 1);
        var order = await PlaceOkAsync(owner);
        var (stranger, _) = await factory.SignedInCustomerAsync(_ct);

        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/orders/{order.Id}", _ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await stranger.PostAsJsonAsync($"/api/orders/{order.Id}/cancel", new CancelOrderRequest(null), _ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.GetAsync("/api/admin/orders", _ct)).StatusCode);
    }

    [Fact]
    public async Task Admin_can_find_orders_by_number_and_filter_by_status()
    {
        using var admin = await factory.SignedInAdminAsync(_ct);
        var product = await ProductAsync(admin);
        var shopper = await ShopperAsync(product.Id, quantity: 1);
        var order = await PlaceOkAsync(shopper);

        var byNumber = await admin.GetFromJsonAsync<PagedResult<AdminOrderSummaryResponse>>(
            $"/api/admin/orders?search={order.OrderNumber}", _ct);
        var placed = await admin.GetFromJsonAsync<PagedResult<AdminOrderSummaryResponse>>(
            "/api/admin/orders?status=Placed&pageSize=100", _ct);
        var delivered = await admin.GetFromJsonAsync<PagedResult<AdminOrderSummaryResponse>>(
            "/api/admin/orders?status=Delivered&pageSize=100", _ct);

        Assert.Equal(order.Id, Assert.Single(byNumber!.Items).Id);
        Assert.Contains(placed!.Items, o => o.Id == order.Id);
        Assert.DoesNotContain(delivered!.Items, o => o.Id == order.Id);

        var mine = await shopper.Client.GetFromJsonAsync<PagedResult<OrderSummaryResponse>>("/api/orders", _ct);
        Assert.Equal(product.Name, Assert.Single(mine!.Items).ItemsPreview);
    }
}

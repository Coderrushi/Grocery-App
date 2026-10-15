using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GroceryStore.Api.Common;
using GroceryStore.Api.Modules.Customers;
using GroceryStore.Api.Modules.Wallet;
using GroceryStore.Tests.Integration.Infrastructure;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GroceryStore.Tests.Integration.Api;

/// <summary>Uses the default limits from appsettings.json: ₹10–₹10,000 per top-up, ₹20,000 maximum balance.</summary>
public sealed class WalletTests(GroceryApiFactory factory)
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private static TopUpRequest TopUp(decimal amount, string? key = null) => new(amount, key ?? Guid.NewGuid().ToString());

    private async Task<TopUpResponse> TopUpOkAsync(HttpClient client, decimal amount, string? key = null)
    {
        var response = await client.PostAsJsonAsync("/api/wallet/top-ups", TopUp(amount, key), _ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TopUpResponse>(_ct))!;
    }

    private async Task<int> UserIdOf(HttpClient client) =>
        (await client.GetFromJsonAsync<GroceryStore.Api.Modules.Identity.CurrentUserResponse>("/api/auth/me", _ct))!.Id;

    private async Task<List<WalletTransactionResponse>> HistoryAsync(HttpClient client) =>
        [.. (await client.GetFromJsonAsync<PagedResult<WalletTransactionResponse>>("/api/wallet/transactions", _ct))!.Items];

    [Fact]
    public async Task A_new_customer_has_an_empty_wallet()
    {
        var (client, _) = await factory.SignedInCustomerAsync(_ct);

        var wallet = await client.GetFromJsonAsync<WalletResponse>("/api/wallet", _ct);

        Assert.Equal(0m, wallet!.Balance);
        Assert.Equal(20_000m, wallet.MaxBalance);
        Assert.Empty(await HistoryAsync(client));
    }

    [Fact]
    public async Task Top_up_credits_the_wallet_and_records_it_in_the_ledger()
    {
        var (client, _) = await factory.SignedInCustomerAsync(_ct);

        var first = await TopUpOkAsync(client, 500m);
        var second = await TopUpOkAsync(client, 250.50m);

        Assert.Equal(TopUpStatus.Succeeded, first.Status);
        Assert.Equal(750.50m, second.Balance);
        var history = await HistoryAsync(client);
        Assert.Equal([(250.50m, 750.50m), (500m, 500m)], history.Select(t => (t.Amount, t.BalanceAfter)));
        Assert.All(history, t => Assert.Equal(WalletTransactionType.TopUp, t.Type));
    }

    [Fact]
    public async Task Repeating_a_top_up_with_the_same_key_never_charges_twice()
    {
        var (client, _) = await factory.SignedInCustomerAsync(_ct);
        var key = Guid.NewGuid().ToString();

        var original = await TopUpOkAsync(client, 300m, key);
        var retry = await TopUpOkAsync(client, 300m, key);
        var concurrent = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => TopUpOkAsync(client, 300m, key)));

        Assert.All(concurrent.Append(retry), r => Assert.Equal(original.Id, r.Id));
        Assert.Equal(300m, (await client.GetFromJsonAsync<WalletResponse>("/api/wallet", _ct))!.Balance);
        Assert.Single(await HistoryAsync(client));
    }

    [Fact]
    public async Task Simultaneous_double_tap_with_one_key_charges_once()
    {
        var (client, _) = await factory.SignedInCustomerAsync(_ct);
        var key = Guid.NewGuid().ToString();

        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => TopUpOkAsync(client, 200m, key)));

        Assert.Single(results.Select(r => r.Id).Distinct());
        Assert.Equal(200m, (await client.GetFromJsonAsync<WalletResponse>("/api/wallet", _ct))!.Balance);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(10_000.01)]
    [InlineData(10.555)]
    [InlineData(-50)]
    public async Task Top_up_amounts_are_validated(decimal amount)
    {
        var (client, _) = await factory.SignedInCustomerAsync(_ct);

        var response = await client.PostAsJsonAsync("/api/wallet/top-ups", TopUp(amount), _ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await response.Content.ReadFromJsonAsync<JsonElement>(_ct)).GetProperty("errors").TryGetProperty("amount", out _));
    }

    [Fact]
    public async Task The_wallet_cannot_be_topped_up_beyond_its_maximum()
    {
        var (client, _) = await factory.SignedInCustomerAsync(_ct);
        await TopUpOkAsync(client, 10_000m);
        await TopUpOkAsync(client, 9_990m);

        var tooMuch = await client.PostAsJsonAsync("/api/wallet/top-ups", TopUp(20m), _ct);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, tooMuch.StatusCode);
        Assert.Contains("₹10.00", await tooMuch.Content.ReadAsStringAsync(_ct), StringComparison.Ordinal); // what's still allowed
    }

    // ---------- Payment gateway failure paths ----------

    private sealed class FakeGateway(Func<int, Task>? duringCharge = null, bool fail = false) : IPaymentGateway
    {
        public List<string> Refunds { get; } = [];
        public string Name => "Fake";

        public async Task<PaymentResult> ChargeAsync(long topUpId, decimal amount, int userId, CancellationToken ct)
        {
            if (duringCharge is not null)
            {
                await duringCharge(userId);
            }

            return fail ? new PaymentResult(false, null, "Card declined") : new PaymentResult(true, $"FAKE-{topUpId}", null);
        }

        public Task RefundAsync(string providerReference, decimal amount, CancellationToken ct)
        {
            Refunds.Add(providerReference);
            return Task.CompletedTask;
        }
    }

    private Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> WithGateway(IPaymentGateway gateway) =>
        factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton(gateway)));

    [Fact]
    public async Task A_declined_payment_leaves_the_wallet_untouched()
    {
        await using var app = WithGateway(new FakeGateway(fail: true));
        using var client = app.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", TestUsers.NewCustomer(), _ct);

        var result = await TopUpOkAsync(client, 500m);

        Assert.Equal(TopUpStatus.Failed, result.Status);
        Assert.Equal("Card declined", result.FailureReason);
        Assert.Equal(0m, result.Balance);
        Assert.Empty(await HistoryAsync(client));
    }

    [Fact]
    public async Task Money_charged_but_not_creditable_is_refunded_never_kept()
    {
        // While the customer is paying, another credit pushes the balance close to the maximum,
        // so the payment succeeds but crediting it would exceed the limit.
        var gateway = new FakeGateway(duringCharge: async userId =>
        {
            await using var db = factory.CreateDbContext();
            await db.Database.ExecuteSqlAsync(
                $"INSERT INTO wallets (UserId, Balance, CreatedAtUtc, UpdatedAtUtc) VALUES ({userId}, 19990, UTC_TIMESTAMP(), UTC_TIMESTAMP()) ON DUPLICATE KEY UPDATE Balance = 19990");
        });
        await using var app = WithGateway(gateway);
        using var client = app.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", TestUsers.NewCustomer(), _ct);

        var result = await TopUpOkAsync(client, 500m);

        Assert.Equal(TopUpStatus.Failed, result.Status);
        Assert.StartsWith("Refunded:", result.FailureReason, StringComparison.Ordinal);
        Assert.Single(gateway.Refunds);
        Assert.Equal(19_990m, result.Balance); // the 500 was not credited
    }

    // ---------- Admin adjustments ----------

    [Fact]
    public async Task Admin_can_credit_and_debit_with_a_recorded_reason()
    {
        var (customer, _) = await factory.SignedInCustomerAsync(_ct);
        var userId = await UserIdOf(customer);
        using var admin = await factory.SignedInAdminAsync(_ct);
        var url = $"/api/admin/customers/{userId}/wallet/adjustments";

        await admin.PostAsJsonAsync(url, new AdminWalletAdjustmentRequest(AdjustmentDirection.Credit, 150m, AdjustmentReason.Refund, "Order 1042: damaged eggs"), _ct);
        var debit = await admin.PostAsJsonAsync(url, new AdminWalletAdjustmentRequest(AdjustmentDirection.Debit, 50m, AdjustmentReason.Correction, "Credited twice by mistake"), _ct);
        var overdraw = await admin.PostAsJsonAsync(url, new AdminWalletAdjustmentRequest(AdjustmentDirection.Debit, 101m, AdjustmentReason.Correction, "Too much"), _ct);
        var noNote = await admin.PostAsJsonAsync(url, new AdminWalletAdjustmentRequest(AdjustmentDirection.Credit, 10m, AdjustmentReason.Goodwill, ""), _ct);
        var byCustomer = await customer.PostAsJsonAsync(url, new AdminWalletAdjustmentRequest(AdjustmentDirection.Credit, 999m, AdjustmentReason.Goodwill, "Free money please"), _ct);

        Assert.Equal(100m, (await debit.Content.ReadFromJsonAsync<AdminWalletResponse>(_ct))!.Balance);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, overdraw.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noNote.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, byCustomer.StatusCode);

        var history = await HistoryAsync(customer);
        Assert.Equal(
            [(WalletTransactionType.AdminDebit, -50m, "Correction: Credited twice by mistake"), (WalletTransactionType.AdminCredit, 150m, "Refund: Order 1042: damaged eggs")],
            history.Select(t => (t.Type, t.Amount, t.Description)));
    }

    [Fact]
    public async Task Concurrent_debits_can_never_overdraw_a_wallet()
    {
        var (customer, _) = await factory.SignedInCustomerAsync(_ct);
        await TopUpOkAsync(customer, 500m);
        var userId = await UserIdOf(customer);
        using var admin = await factory.SignedInAdminAsync(_ct);

        var responses = await Task.WhenAll(Enumerable.Range(0, 10).Select(i =>
            admin.PostAsJsonAsync($"/api/admin/customers/{userId}/wallet/adjustments",
                new AdminWalletAdjustmentRequest(AdjustmentDirection.Debit, 100m, AdjustmentReason.Correction, $"Concurrent debit {i}"), _ct)));

        Assert.Equal(5, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(5, responses.Count(r => r.StatusCode == HttpStatusCode.UnprocessableEntity));
        Assert.Equal(0m, (await customer.GetFromJsonAsync<WalletResponse>("/api/wallet", _ct))!.Balance);
    }

    [Fact]
    public async Task Every_wallet_balance_equals_the_sum_of_its_ledger()
    {
        // Runs over the whole test database, so it also checks every other wallet test's data.
        var (customer, _) = await factory.SignedInCustomerAsync(_ct);
        await TopUpOkAsync(customer, 120m);
        await using var db = factory.CreateDbContext();

        var mismatches = await db.Database.SqlQuery<int>($"""
            SELECT w.UserId AS Value
            FROM wallets w
            LEFT JOIN (SELECT UserId, SUM(Amount) AS Total FROM wallet_transactions GROUP BY UserId) t ON t.UserId = w.UserId
            WHERE w.Balance <> COALESCE(t.Total, 0)
            """).ToListAsync(_ct);

        // The refund test forces one wallet's balance with raw SQL to stage a race; that wallet has no
        // ledger rows at all, so only wallets that have ledger entries are checked.
        var withLedger = await db.Set<WalletTransaction>().Select(t => t.UserId).Distinct().ToListAsync(_ct);
        Assert.Empty(mismatches.Intersect(withLedger));
    }

    [Fact]
    public async Task The_database_refuses_to_apply_the_same_event_twice()
    {
        var (customer, _) = await factory.SignedInCustomerAsync(_ct);
        var topUp = await TopUpOkAsync(customer, 100m);
        var userId = await UserIdOf(customer);
        await using var db = factory.CreateDbContext();

        var error = await Assert.ThrowsAsync<MySql.Data.MySqlClient.MySqlException>(() => db.Database.ExecuteSqlAsync($"""
            INSERT INTO wallet_transactions (UserId, Type, Amount, BalanceAfter, Description, Reference, CreatedAtUtc)
            VALUES ({userId}, 'TopUp', 100, 200, 'Replay', {"topup:" + topUp.Id}, UTC_TIMESTAMP())
            """, _ct));
        Assert.Equal(1062, error.Number); // duplicate key: specifically the unique Reference, not some other failure
    }

    [Fact]
    public async Task Admin_customer_list_shows_wallet_balances()
    {
        var (customer, registered) = await factory.SignedInCustomerAsync(_ct);
        await TopUpOkAsync(customer, 345m);
        using var admin = await factory.SignedInAdminAsync(_ct);

        var page = await admin.GetFromJsonAsync<PagedResult<CustomerSummaryResponse>>(
            $"/api/admin/customers?search={registered.PhoneNumber}", _ct);

        Assert.Equal(345m, Assert.Single(page!.Items).WalletBalance);
    }
}

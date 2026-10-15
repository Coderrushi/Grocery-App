using Microsoft.EntityFrameworkCore;

namespace GroceryStore.Tests.Integration.ProviderSpike;

/// <summary>
/// Pins down the behaviour of MySql.EntityFrameworkCore that the application depends on.
/// If a provider upgrade breaks any of these, it would break checkout, the wallet or reports —
/// so these run on every build. Findings are summarised in docs/adr/0001-mysql-ef-core-provider.md.
/// </summary>
public sealed class MySqlProviderTests(SpikeDatabase database) : IClassFixture<SpikeDatabase>
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private async Task<int> AddProductAsync(string name, decimal price = 10m, int stock = 10)
    {
        await using var db = database.Create();
        var product = new SpikeProduct { Name = name, Price = price, Stock = stock, UpdatedAtUtc = DateTime.UtcNow };
        db.SpikeProducts.Add(product);
        await db.SaveChangesAsync(_ct);
        return product.Id;
    }

    // ---------- Data types ----------

    [Fact]
    public async Task Unicode_text_and_money_round_trip_exactly()
    {
        var id = await AddProductAsync("बासमती चावल (Basmati) 🍚", price: 99_999_999.99m);

        await using var db = database.Create();
        var product = await db.SpikeProducts.SingleAsync(p => p.Id == id, _ct);

        Assert.Equal("बासमती चावल (Basmati) 🍚", product.Name);
        Assert.Equal(99_999_999.99m, product.Price);
    }

    [Fact]
    public async Task Extra_decimal_places_are_rounded_half_away_from_zero_by_mysql()
    {
        // Documents the database behaviour; application code rounds explicitly before saving anyway.
        var id = await AddProductAsync("Rounding", price: 10.005m);

        await using var db = database.Create();
        Assert.Equal(10.01m, await db.SpikeProducts.Where(p => p.Id == id).Select(p => p.Price).SingleAsync(_ct));
    }

    [Fact]
    public async Task Utc_timestamps_round_trip_with_microseconds_and_utc_kind()
    {
        var instant = new DateTime(2026, 10, 15, 8, 30, 15, DateTimeKind.Utc).AddTicks(1234560); // .123456 s
        int id;
        await using (var db = database.Create())
        {
            var order = new SpikeOrder { PlacedAtUtc = instant, Total = 1m };
            db.SpikeOrders.Add(order);
            await db.SaveChangesAsync(_ct);
            id = order.Id;
        }

        await using var verify = database.Create();
        var loaded = await verify.SpikeOrders.SingleAsync(o => o.Id == id, _ct);

        Assert.Equal(instant, loaded.PlacedAtUtc);
        Assert.Equal(DateTimeKind.Utc, loaded.PlacedAtUtc.Kind);
    }

    // ---------- Query translation ----------

    [Fact]
    public async Task Text_search_is_case_insensitive()
    {
        await AddProductAsync("Amul Taaza Toned MILK 1L");

        await using var db = database.Create();
        var found = await db.SpikeProducts.Where(p => p.Name.Contains("milk")).Select(p => p.Name).ToListAsync(_ct);

        Assert.Contains("Amul Taaza Toned MILK 1L", found);
    }

    [Fact]
    public async Task Daily_sales_report_query_translates_to_sql()
    {
        var day = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        await using (var db = database.Create())
        {
            db.SpikeOrders.AddRange(
                new SpikeOrder { PlacedAtUtc = day.AddHours(9), Total = 0.10m },
                new SpikeOrder { PlacedAtUtc = day.AddHours(18), Total = 0.20m },
                new SpikeOrder { PlacedAtUtc = day.AddDays(1).AddHours(1), Total = 5m });
            await db.SaveChangesAsync(_ct);
        }

        await using var query = database.Create();
        var report = await query.SpikeOrders
            .Where(o => o.PlacedAtUtc >= day && o.PlacedAtUtc < day.AddDays(2))
            .GroupBy(o => o.PlacedAtUtc.Date)
            .Select(g => new { Day = g.Key, Revenue = g.Sum(o => o.Total), Orders = g.Count() })
            .OrderBy(r => r.Day)
            .ToListAsync(_ct);

        Assert.Equal(2, report.Count);
        Assert.Equal(0.30m, report[0].Revenue); // decimal arithmetic, no floating-point drift
        Assert.Equal(2, report[0].Orders);
        Assert.Equal(5m, report[1].Revenue);
    }

    [Fact]
    public async Task Paging_with_skip_and_take_translates_to_sql()
    {
        for (var i = 0; i < 5; i++)
        {
            await AddProductAsync($"Paging-{i}");
        }

        await using var db = database.Create();
        var page = await db.SpikeProducts
            .Where(p => p.Name.StartsWith("Paging-"))
            .OrderBy(p => p.Name)
            .Skip(2).Take(2)
            .Select(p => p.Name)
            .ToListAsync(_ct);

        Assert.Equal(["Paging-2", "Paging-3"], page);
    }

    // ---------- Transactions ----------

    [Fact]
    public async Task Explicit_transaction_must_run_inside_the_execution_strategy()
    {
        await using var db = database.Create();

        // With EnableRetryOnFailure, the first query inside a bare transaction is rejected because a
        // retry could not safely replay the whole transaction. This is the rule every multi-step
        // write in the app must follow.
        var error = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(_ct);
            await db.SpikeProducts.CountAsync(_ct);
        });
        Assert.Contains("user-initiated transactions", error.Message, StringComparison.Ordinal);

        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(_ct);
            await tx.CommitAsync(_ct);
        });
    }

    [Fact]
    public async Task Rolled_back_transaction_leaves_no_trace()
    {
        var id = await AddProductAsync("Rollback", stock: 10);

        await using (var db = database.Create())
        {
            await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var tx = await db.Database.BeginTransactionAsync(_ct);
                var product = await db.SpikeProducts.SingleAsync(p => p.Id == id, _ct);
                product.Stock = 0;
                db.SpikeOrders.Add(new SpikeOrder { PlacedAtUtc = DateTime.UtcNow, Total = 123.45m });
                await db.SaveChangesAsync(_ct);
                await tx.RollbackAsync(_ct);
            });
        }

        await using var verify = database.Create();
        Assert.Equal(10, await verify.SpikeProducts.Where(p => p.Id == id).Select(p => p.Stock).SingleAsync(_ct));
        Assert.False(await verify.SpikeOrders.AnyAsync(o => o.Total == 123.45m, _ct));
    }

    // ---------- Concurrency: the checkout problem ----------

    [Fact]
    public async Task Select_for_update_prevents_overselling_under_concurrent_checkouts()
    {
        const int stock = 5;
        const int buyers = 12;
        var id = await AddProductAsync("Last few mangoes", stock: stock);

        async Task<bool> TryBuyOneAsync()
        {
            await using var db = database.Create();
            return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var tx = await db.Database.BeginTransactionAsync(_ct);

                // Row lock: other buyers wait here until this transaction commits.
                var product = await db.SpikeProducts
                    .FromSql($"SELECT * FROM spike_products WHERE Id = {id} FOR UPDATE")
                    .SingleAsync(_ct);

                if (product.Stock < 1)
                {
                    await tx.RollbackAsync(_ct);
                    return false;
                }

                product.Stock -= 1;
                db.SpikeOrders.Add(new SpikeOrder { PlacedAtUtc = DateTime.UtcNow, Total = product.Price });
                await db.SaveChangesAsync(_ct);
                await tx.CommitAsync(_ct);
                return true;
            });
        }

        var results = await Task.WhenAll(Enumerable.Range(0, buyers).Select(_ => Task.Run(TryBuyOneAsync, _ct)));

        Assert.Equal(stock, results.Count(bought => bought));
        await using var verify = database.Create();
        Assert.Equal(0, await verify.SpikeProducts.Where(p => p.Id == id).Select(p => p.Stock).SingleAsync(_ct));
    }

    [Fact]
    public async Task Conditional_bulk_update_also_prevents_overselling()
    {
        // Alternative to FOR UPDATE for simple decrements: a single atomic UPDATE ... WHERE Stock >= qty.
        const int stock = 5;
        var id = await AddProductAsync("Last few apples", stock: stock);

        async Task<bool> TryBuyOneAsync()
        {
            await using var db = database.Create();
            var affected = await db.SpikeProducts
                .Where(p => p.Id == id && p.Stock >= 1)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock - 1), _ct);
            return affected == 1;
        }

        var results = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => Task.Run(TryBuyOneAsync, _ct)));

        Assert.Equal(stock, results.Count(bought => bought));
        await using var verify = database.Create();
        Assert.Equal(0, await verify.SpikeProducts.Where(p => p.Id == id).Select(p => p.Stock).SingleAsync(_ct));
    }

    [Fact]
    public async Task Concurrency_token_detects_lost_updates()
    {
        // Admin scenario: two staff members edit the same product price at the same time.
        var id = await AddProductAsync("Optimistic", price: 50m);

        await using var first = database.Create();
        await using var second = database.Create();
        var a = await first.SpikeProducts.SingleAsync(p => p.Id == id, _ct);
        var b = await second.SpikeProducts.SingleAsync(p => p.Id == id, _ct);

        a.Price = 55m;
        a.Version++;
        await first.SaveChangesAsync(_ct);

        b.Price = 45m;
        b.Version++;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync(_ct));
    }
}

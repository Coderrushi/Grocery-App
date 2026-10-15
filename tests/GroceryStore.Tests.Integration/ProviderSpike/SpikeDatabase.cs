using GroceryStore.Tests.Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GroceryStore.Tests.Integration.ProviderSpike;

public sealed class SpikeDatabase : IAsyncLifetime
{
    private readonly string _connectionString = TestDatabase.ConnectionStringFor("grocery_test_spike");

    /// <summary>Configured exactly like the application: with the retrying execution strategy.</summary>
    public SpikeDbContext Create() => new(new DbContextOptionsBuilder<SpikeDbContext>()
        .UseMySQL(_connectionString, o => o.EnableRetryOnFailure(3))
        .Options);

    public async ValueTask InitializeAsync()
    {
        await TestDatabase.RecreateAsync(_connectionString);
        await using var db = Create();
        await db.Database.EnsureCreatedAsync();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

using GroceryStore.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

[assembly: AssemblyFixture(typeof(GroceryStore.Tests.Integration.Infrastructure.GroceryApiFactory))]

namespace GroceryStore.Tests.Integration.Infrastructure;

/// <summary>
/// Hosts the real API in memory against a freshly migrated grocery_test database.
/// Shared by all API tests in the assembly (one database setup per test run).
/// </summary>
public sealed class GroceryApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string DatabaseName = "grocery_test";

    private readonly string _connectionString = TestDatabase.ConnectionStringFor(DatabaseName);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // "Testing" (not "Development") so the developer's user-secrets — which point at grocery_dev —
        // are never loaded into the app under test.
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:GroceryDb", _connectionString);
    }

    public async ValueTask InitializeAsync()
    {
        await TestDatabase.RecreateAsync(_connectionString);

        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

    public AppDbContext CreateDbContext() =>
        Services.CreateScope().ServiceProvider.GetRequiredService<AppDbContext>();
}

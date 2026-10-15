using GroceryStore.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

[assembly: AssemblyFixture(typeof(GroceryStore.Tests.Integration.Infrastructure.GroceryApiFactory))]

namespace GroceryStore.Tests.Integration.Infrastructure;

/// <summary>
/// Hosts the real API in memory against a freshly migrated grocery_test database.
/// Shared by all API tests in the assembly (one database setup per test run), so tests must not
/// depend on a clean database: each creates its own uniquely named users.
/// </summary>
public sealed class GroceryApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string DatabaseName = "grocery_test";
    public const string AdminEmail = "admin@quickkirana.test";
    public const string AdminPassword = "admin-test-password";

    private readonly string _connectionString = TestDatabase.ConnectionStringFor(DatabaseName);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // "Testing" (not "Development") so the developer's user-secrets — which point at grocery_dev —
        // are never loaded into the app under test.
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:GroceryDb", _connectionString);
        builder.UseSetting("AdminSeed:Email", AdminEmail);
        builder.UseSetting("AdminSeed:Password", AdminPassword);
        builder.UseSetting("AdminSeed:FullName", "Test Admin");
        // Every test client shares one "IP"; the rate limit itself is tested with a dedicated host.
        builder.UseSetting("RateLimiting:AuthPermitLimitPerMinute", "100000");
    }

    public async ValueTask InitializeAsync()
    {
        await TestDatabase.RecreateAsync(_connectionString);

        // Migrate BEFORE the host starts: start-up seeds the admin account and needs the tables.
        var options = new DbContextOptionsBuilder<AppDbContext>().UseMySQL(_connectionString).Options;
        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();

        _ = Server; // start the host now (runs the admin seeder) rather than inside the first test
    }

    public AppDbContext CreateDbContext() =>
        Services.CreateScope().ServiceProvider.GetRequiredService<AppDbContext>();
}

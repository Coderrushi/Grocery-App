using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GroceryStore.Api.Data.Conventions;
using GroceryStore.Api.Modules.Identity;
using GroceryStore.Tests.Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GroceryStore.Tests.Integration.Api;

public sealed class FoundationTests(GroceryApiFactory factory)
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Tests_run_against_the_test_database_never_the_dev_database()
    {
        await using var db = factory.CreateDbContext();
        Assert.Equal(GroceryApiFactory.DatabaseName, db.Database.GetDbConnection().Database);
    }

    [Fact]
    public async Task Health_endpoint_reports_api_and_database_healthy()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health", _ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(_ct);
        Assert.Equal("Healthy", body.GetProperty("status").GetString());
        Assert.Equal("Healthy", body.GetProperty("checks").GetProperty("database").GetString());
    }

    [Fact]
    public async Task Unknown_route_returns_problem_details()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/does-not-exist", _ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(_ct);
        Assert.Equal(404, body.GetProperty("status").GetInt32());
        Assert.True(body.TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task Model_has_no_changes_missing_from_migrations()
    {
        await using var db = factory.CreateDbContext();
        Assert.False(db.Database.HasPendingModelChanges(),
            "The EF model has changed since the last migration. Run `dotnet ef migrations add <Name>`.");
        Assert.Empty(await db.Database.GetPendingMigrationsAsync(_ct));
    }

    [Fact]
    public async Task Roles_are_seeded()
    {
        await using var db = factory.CreateDbContext();
        var roles = await db.Roles.Select(r => r.Name).OrderBy(n => n).ToListAsync(_ct);
        Assert.Equal([Roles.Admin, Roles.Customer], roles);
    }

    [Fact]
    public async Task Every_table_uses_utf8mb4_collation()
    {
        await using var db = factory.CreateDbContext();

        var wrong = await db.Database.SqlQuery<string>($"""
            SELECT CONCAT(TABLE_NAME, ' = ', TABLE_COLLATION) AS Value
            FROM information_schema.TABLES
            WHERE TABLE_SCHEMA = {GroceryApiFactory.DatabaseName}
              AND TABLE_COLLATION <> {MySqlModelConventions.Collation}
            """).ToListAsync(_ct);

        Assert.Empty(wrong);
    }
}

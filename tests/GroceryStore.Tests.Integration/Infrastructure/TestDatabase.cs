using GroceryStore.Api.Data.Conventions;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;

namespace GroceryStore.Tests.Integration.Infrastructure;

/// <summary>
/// Locates the test MySQL server and (re)creates throwaway databases on it.
/// Connection string source, in order: environment variable ConnectionStrings__GroceryTestDb (CI),
/// then the API project's user-secrets key ConnectionStrings:GroceryTestDb (local development).
/// </summary>
internal static class TestDatabase
{
    private const string RequiredPrefix = "grocery_test";

    public static string ConnectionStringFor(string databaseName)
    {
        // Safety net: these helpers DROP databases. Refuse anything that is not clearly a test database.
        if (!databaseName.StartsWith(RequiredPrefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Refusing to use '{databaseName}': test databases must start with '{RequiredPrefix}'.");
        }

        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<Program>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var baseConnectionString = configuration.GetConnectionString("GroceryTestDb");
        if (string.IsNullOrWhiteSpace(baseConnectionString))
        {
            throw new InvalidOperationException(
                "No test database configured. Set ConnectionStrings:GroceryTestDb in the API project's user-secrets " +
                "or the ConnectionStrings__GroceryTestDb environment variable. See README.md.");
        }

        return new MySqlConnectionStringBuilder(baseConnectionString) { Database = databaseName }.ConnectionString;
    }

    /// <summary>Drops and recreates an empty database with the application's character set and collation.</summary>
    public static async Task RecreateAsync(string connectionString)
    {
        var builder = new MySqlConnectionStringBuilder(connectionString);
        var databaseName = builder.Database;
        if (!databaseName.StartsWith(RequiredPrefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Refusing to drop '{databaseName}'.");
        }

        builder.Database = string.Empty;
        await using var connection = new MySqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
#pragma warning disable CA2100 // The name is validated above and comes from test code, never user input.
        command.CommandText =
            $"DROP DATABASE IF EXISTS `{databaseName}`; " +
            $"CREATE DATABASE `{databaseName}` CHARACTER SET {MySqlModelConventions.CharSet} COLLATE {MySqlModelConventions.Collation};";
#pragma warning restore CA2100
        await command.ExecuteNonQueryAsync();
    }
}

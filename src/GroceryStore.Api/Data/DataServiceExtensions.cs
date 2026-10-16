using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;

namespace GroceryStore.Api.Data;

/// <summary>Database settings beyond the connection string ("Database" in appsettings).</summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>
    /// Apply pending EF migrations when the app starts. For a single instance without a pre-deploy step
    /// (Render's free plan). EF Core takes a database lock while migrating, so a concurrent start waits.
    /// </summary>
    public bool MigrateOnStartup { get; set; }

    /// <summary>
    /// The database server's CA certificate (PEM text, e.g. Aiven's ca.pem). When set, the connection
    /// verifies the server certificate (SslMode=VerifyCA) instead of only encrypting, which stops a
    /// man-in-the-middle on the public internet.
    /// </summary>
    public string? CaCertificatePem { get; set; }
}

internal static class DataServiceExtensions
{
    public const string ConnectionStringName = "GroceryDb";

    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' is not configured. In development, set it with " +
                $"`dotnet user-secrets set \"ConnectionStrings:{ConnectionStringName}\" \"<value>\"` " +
                "in src/GroceryStore.Api; in production, use the ConnectionStrings__GroceryDb environment variable.");
        }

        var options = configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>() ?? new DatabaseOptions();
        services.AddSingleton(options);
        connectionString = WithVerifiedTls(connectionString, options.CaCertificatePem);

        services.AddDbContext<AppDbContext>(db =>
            db.UseMySQL(connectionString, mysql =>
            {
                // Retries transient failures. Consequence: code that opens its own transaction must run
                // inside db.Database.CreateExecutionStrategy().ExecuteAsync(...), and must rebuild its
                // state at the start of each attempt (see OrderService).
                // The Oracle provider does not treat deadlocks as transient by default (found in Phase 6),
                // so they are added explicitly: 1213 = deadlock, 1205 = lock wait timeout.
                mysql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(2), errorNumbersToAdd: [1213, 1205]);
            }));

        return services;
    }

    /// <summary>The MySQL driver reads a CA from a file, so the PEM from configuration is written to one.</summary>
    internal static string WithVerifiedTls(string connectionString, string? caCertificatePem)
    {
        if (string.IsNullOrWhiteSpace(caCertificatePem))
        {
            return connectionString;
        }

        var caFile = Path.Combine(Path.GetTempPath(), "quickkirana-db-ca.pem");
        File.WriteAllText(caFile, caCertificatePem.Replace("\\n", "\n", StringComparison.Ordinal)); // env vars often carry "\n"
        return new MySqlConnectionStringBuilder(connectionString) { SslMode = MySqlSslMode.VerifyCA, SslCa = caFile }.ConnectionString;
    }

    /// <summary>Applies pending migrations when Database:MigrateOnStartup is on (before seeding, which needs the tables).</summary>
    public static async Task MigrateDatabaseIfEnabledAsync(this WebApplication app)
    {
        if (!app.Services.GetRequiredService<DatabaseOptions>().MigrateOnStartup)
        {
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }
}

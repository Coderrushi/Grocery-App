using Microsoft.EntityFrameworkCore;

namespace GroceryStore.Api.Data;

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

        services.AddDbContext<AppDbContext>(options =>
            options.UseMySQL(connectionString, mysql =>
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
}

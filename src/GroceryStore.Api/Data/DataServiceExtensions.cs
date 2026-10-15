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
                // Retries transient failures (dropped connections, deadlocks). Consequence: code that opens
                // its own transaction must run inside db.Database.CreateExecutionStrategy().ExecuteAsync(...).
                mysql.EnableRetryOnFailure(maxRetryCount: 3);
            }));

        return services;
    }
}

using System.Text;
using Microsoft.EntityFrameworkCore;

namespace GroceryStore.Api.Data.Conventions;

/// <summary>
/// Database-wide conventions shared by every DbContext that targets MySQL (including test contexts),
/// so the rules are defined once.
/// </summary>
public static class MySqlModelConventions
{
    public const string CharSet = "utf8mb4";
    public const string Collation = "utf8mb4_0900_ai_ci";

    /// <summary>Call from ConfigureConventions.</summary>
    public static void Apply(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>().HavePrecision(6);

        // Money: two decimal places, up to 99,999,999.99. Avoids the provider default of decimal(65,30)
        // and the silent truncation warnings EF raises for unconfigured decimals.
        configurationBuilder.Properties<decimal>().HavePrecision(10, 2);
    }

    /// <summary>Call at the END of OnModelCreating, after all entity configurations are applied.</summary>
    public static void Apply(ModelBuilder modelBuilder)
    {
        // Character set and collation (utf8mb4 / utf8mb4_0900_ai_ci) are deliberately NOT declared here.
        // Tables inherit them from the database, which is created with them explicitly (database/dev-setup.sql,
        // and MySQL 8's server default). Declaring them in the model makes the provider generate migration
        // code that does not compile (ADR 0001). An integration test verifies every table's collation.

        // MySQL table names are case-sensitive on Linux but not on Windows. Lower-case snake_case
        // names behave identically everywhere, so a Windows dev box and a Linux server never disagree.
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            if (entity.GetTableName() is { } tableName && entity.BaseType is null)
            {
                // Identity's "AspNetUsers" becomes "users", "AspNetUserRoles" becomes "user_roles", etc.
                const string identityPrefix = "AspNet";
                if (tableName.StartsWith(identityPrefix, StringComparison.Ordinal))
                {
                    tableName = tableName[identityPrefix.Length..];
                }

                entity.SetTableName(ToSnakeCase(tableName));
            }
        }
    }

    internal static string ToSnakeCase(string name)
    {
        var builder = new StringBuilder(name.Length + 8);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c))
            {
                var startsNewWord = i > 0 && name[i - 1] != '_' &&
                    (char.IsLower(name[i - 1]) || (i + 1 < name.Length && char.IsLower(name[i + 1])));
                if (startsNewWord)
                {
                    builder.Append('_');
                }

                builder.Append(char.ToLowerInvariant(c));
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }
}

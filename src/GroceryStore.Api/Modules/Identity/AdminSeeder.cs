using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace GroceryStore.Api.Modules.Identity;

/// <summary>
/// The shopkeeper's admin account. Its password must never live in source control or a migration, so it
/// is created at start-up from configuration: user-secrets in development, environment variables
/// (AdminSeed__Email, AdminSeed__Password, ...) for the first production start. Once the account exists,
/// the settings can be removed; the seeder never changes an existing password.
/// </summary>
public sealed class AdminSeedOptions
{
    public const string SectionName = "AdminSeed";

    public string? Email { get; init; }
    public string? Password { get; init; }
    public string FullName { get; init; } = "Store Admin";
    public string? PhoneNumber { get; init; }
}

internal static partial class AdminSeeder
{
    public static async Task SeedAdminAsync(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<IOptions<AdminSeedOptions>>().Value;
        if (string.IsNullOrWhiteSpace(options.Email) || string.IsNullOrWhiteSpace(options.Password))
        {
            return; // Not configured: nothing to do.
        }

        await using var scope = app.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AdminSeedOptions>>();
        var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        var user = await userManager.FindByEmailAsync(options.Email);
        if (user is null)
        {
            user = new AppUser
            {
                FullName = options.FullName,
                Email = options.Email,
                UserName = options.Email,
                PhoneNumber = options.PhoneNumber,
                EmailConfirmed = true,
                CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime,
            };
            (await userManager.CreateAsync(user, options.Password)).ThrowIfFailed();
            LogAdminCreated(logger, options.Email);
        }

        if (!await userManager.IsInRoleAsync(user, Roles.Admin))
        {
            (await userManager.AddToRoleAsync(user, Roles.Admin)).ThrowIfFailed();
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Created admin account {Email}")]
    private static partial void LogAdminCreated(ILogger logger, string email);
}

using GroceryStore.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace GroceryStore.Api.Modules.Identity;

internal static class IdentitySetup
{
    public const string AuthCookieName = "qk_auth";

    public static IServiceCollection AddAppIdentity(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();

        services.AddIdentityCore<AppUser>(options =>
            {
                options.User.RequireUniqueEmail = true;

                // NIST SP 800-63B: length matters; composition rules ("1 symbol, 1 capital") mostly
                // produce predictable passwords like "Password@1" and frustrate non-technical customers.
                options.Password.RequiredLength = 8;
                options.Password.RequiredUniqueChars = 4;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;

                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;
            })
            .AddRoles<AppRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = AuthCookieName;
            options.Cookie.HttpOnly = true; // JavaScript (and therefore XSS) can never read it
            // Strict: the browser never attaches the cookie to requests started by another site, which
            // blocks CSRF. Safe for an SPA because the HTML is public and only same-site fetches need it.
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = environment.IsProduction()
                ? CookieSecurePolicy.Always
                : CookieSecurePolicy.SameAsRequest; // local http and the in-memory test server
            options.ExpireTimeSpan = TimeSpan.FromDays(14);
            options.SlidingExpiration = true;

            // An API must answer 401/403, not redirect to an MVC login page.
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });

        // Deny by default: an endpoint without [AllowAnonymous] requires a signed-in user, so forgetting an
        // attribute fails closed. Back-office endpoints additionally require the ManageStore policy.
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(Policies.ManageStore, policy => policy.RequireRole(Roles.Admin));

        services.AddOptions<AdminSeedOptions>()
            .BindConfiguration(AdminSeedOptions.SectionName)
            // A production admin password is the key to every customer's data and wallet.
            .Validate(o => !environment.IsProduction() || o.Password is null || o.Password.Length >= AdminSeedOptions.MinProductionPasswordLength,
                $"AdminSeed:Password must be at least {AdminSeedOptions.MinProductionPasswordLength} characters in production.")
            .ValidateOnStart();
        services.AddScoped<AuthService>();
        services.AddScoped<AccountService>();

        return services;
    }
}

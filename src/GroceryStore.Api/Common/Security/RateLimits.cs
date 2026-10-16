using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace GroceryStore.Api.Common.Security;

/// <summary>Requests per minute for each limit ("RateLimiting" in appsettings). All fixed one-minute windows.</summary>
public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>
    /// Every /api request, per client IP. Generous on purpose: Indian mobile networks put many customers
    /// behind one carrier-grade NAT address, so this only stops floods, not shoppers.
    /// </summary>
    public int GlobalPermitLimitPerMinute { get; set; } = 600;

    /// <summary>Sign-in and registration, per IP: limits password guessing across many accounts.</summary>
    public int AuthPermitLimitPerMinute { get; set; } = 10;

    /// <summary>Money and account changes (top-ups, orders, password, bulk updates), per signed-in user.</summary>
    public int SensitivePermitLimitPerMinute { get; set; } = 20;

    /// <summary>Report pages and CSV exports (heavier queries), per signed-in user.</summary>
    public int ReportsPermitLimitPerMinute { get; set; } = 30;
}

/// <summary>Named rate-limit policies for [EnableRateLimiting(...)], plus the global per-IP limit.</summary>
public static class RateLimits
{
    public const string Auth = "auth";
    public const string Sensitive = "sensitive";
    public const string Reports = "reports";

    public static IServiceCollection AddAppRateLimiting(this IServiceCollection services)
    {
        services.AddOptions<RateLimitingOptions>()
            .BindConfiguration(RateLimitingOptions.SectionName)
            .Validate(o => o.GlobalPermitLimitPerMinute > 0 && o.AuthPermitLimitPerMinute > 0
                && o.SensitivePermitLimitPerMinute > 0 && o.ReportsPermitLimitPerMinute > 0,
                "RateLimiting limits must be positive.")
            .ValidateOnStart();

        return services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = WriteTooManyRequestsAsync;

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                // Health checks are polled by the hosting platform and must never be throttled.
                context.Request.Path.StartsWithSegments("/api") && !context.Request.Path.StartsWithSegments("/api/health")
                    ? PerMinute($"ip:{ClientIp(context)}", Limits(context).GlobalPermitLimitPerMinute)
                    : RateLimitPartition.GetNoLimiter("unlimited"));

            options.AddPolicy(Auth, context => PerMinute($"ip:{ClientIp(context)}", Limits(context).AuthPermitLimitPerMinute));
            options.AddPolicy(Sensitive, context => PerMinute(UserOrIp(context), Limits(context).SensitivePermitLimitPerMinute));
            options.AddPolicy(Reports, context => PerMinute(UserOrIp(context), Limits(context).ReportsPermitLimitPerMinute));
        });
    }

    private static RateLimitingOptions Limits(HttpContext context) =>
        context.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value;

    // The real client address, once UseForwardedHeaders has applied a trusted proxy's X-Forwarded-For.
    private static string ClientIp(HttpContext context) => context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private static string UserOrIp(HttpContext context) =>
        context.User.GetUserIdOrNull() is { } userId ? $"user:{userId}" : $"ip:{ClientIp(context)}";

    private static RateLimitPartition<string> PerMinute(string key, int permits) =>
        RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permits,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        });

    /// <summary>A ProblemDetails body like every other error, and Retry-After so clients know when to try again.</summary>
    private static async ValueTask WriteTooManyRequestsAsync(OnRejectedContext context, CancellationToken ct)
    {
        var seconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
            ? Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds))
            : 60;
        var http = context.HttpContext;
        http.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);

        var problems = http.RequestServices.GetRequiredService<IProblemDetailsService>();
        await problems.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Too many requests",
                Detail = $"You're doing that too often. Please wait {seconds} seconds and try again.",
            },
        });
    }
}

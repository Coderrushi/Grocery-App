using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GroceryStore.Api.Common.Security;
using GroceryStore.Api.Modules.Identity;
using GroceryStore.Api.Modules.Wallet;
using GroceryStore.Tests.Integration.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace GroceryStore.Tests.Integration.Api;

/// <summary>Phase 8 hardening (ADR 0004): authorization by default, headers, limits, keys and start-up checks.</summary>
public sealed class SecurityTests(GroceryApiFactory factory)
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    // ---------- Authorization audit ----------

    private sealed record RouteInfo(string Route, bool Anonymous, IReadOnlyList<string?> Policies);

    private List<RouteInfo> Routes() =>
        [.. factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Select(e => new RouteInfo(
                $"{string.Join(",", e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["*"])} /{e.RoutePattern.RawText!.TrimStart('/')}",
                e.Metadata.GetMetadata<IAllowAnonymous>() is not null,
                [.. e.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(a => a.Policy)]))];

    /// <summary>
    /// Everything is private unless listed here. A new public endpoint fails this test until it is added
    /// on purpose — so "forgot [Authorize]" can't ship.
    /// </summary>
    [Fact]
    public void Only_the_storefront_sign_in_and_health_endpoints_are_public()
    {
        var anonymous = Routes().Where(r => r.Anonymous).Select(r => r.Route).Order(StringComparer.Ordinal).ToList();

        Assert.Equal(
            [
                "* /api/health", // health checks answer any method
                "* /api/health/live",
                "* /api/health/ready",
                "GET /api/auth/me", // answers 204 for visitors
                "GET /api/catalog/categories",
                "GET /api/catalog/products",
                "GET /api/catalog/products/{id:int}",
                "POST /api/auth/login",
                "POST /api/auth/logout",
                "POST /api/auth/register",
                "POST /api/cart/preview",
            ],
            anonymous);
    }

    [Fact]
    public void Every_back_office_route_requires_the_manage_store_policy()
    {
        var admin = Routes().Where(r => r.Route.Contains(" /api/admin/", StringComparison.Ordinal)).ToList();

        Assert.NotEmpty(admin);
        Assert.All(admin, r =>
        {
            Assert.False(r.Anonymous, r.Route);
            Assert.Contains(Policies.ManageStore, r.Policies);
        });
    }

    [Fact]
    public async Task Endpoints_without_an_attribute_are_closed_to_visitors()
    {
        // The fallback policy is what protects an endpoint someone forgot to annotate.
        var fallback = await factory.Services.GetRequiredService<IAuthorizationPolicyProvider>().GetFallbackPolicyAsync();

        Assert.NotNull(fallback);
        Assert.Contains(fallback.Requirements, r => r is DenyAnonymousAuthorizationRequirement);
    }

    // ---------- Headers ----------

    [Theory]
    [InlineData("/api/catalog/categories", HttpStatusCode.OK)]
    [InlineData("/api/orders", HttpStatusCode.Unauthorized)]
    [InlineData("/api/does-not-exist", HttpStatusCode.NotFound)]
    public async Task Every_api_response_carries_the_security_headers(string url, HttpStatusCode expected)
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(url, _ct);

        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("nosniff", Header(response, "X-Content-Type-Options"));
        Assert.Equal("DENY", Header(response, "X-Frame-Options"));
        Assert.Equal(SecurityHeaders.ApiContentSecurityPolicy, Header(response, "Content-Security-Policy"));
        Assert.Equal("strict-origin-when-cross-origin", Header(response, "Referrer-Policy"));
        Assert.Contains("camera=()", Header(response, "Permissions-Policy"), StringComparison.Ordinal);
        Assert.True(response.Headers.CacheControl?.NoStore, "API responses must not be cached");
    }

    private static string Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) ? string.Join(",", values) : "(missing)";

    // ---------- Health ----------

    [Theory]
    [InlineData("/api/health/live", false)]
    [InlineData("/api/health/ready", true)]
    public async Task Liveness_has_no_dependencies_and_readiness_checks_the_database(string url, bool checksDatabase)
    {
        using var client = factory.CreateClient();

        var body = await client.GetFromJsonAsync<JsonElement>(url, _ct);

        Assert.Equal("Healthy", body.GetProperty("status").GetString());
        Assert.Equal(checksDatabase, body.GetProperty("checks").TryGetProperty("database", out _));
    }

    // ---------- Rate limits ----------

    [Fact]
    public async Task Money_endpoints_are_limited_per_user_with_a_retry_after()
    {
        await using var limited = factory.WithWebHostBuilder(b => b.UseSetting("RateLimiting:SensitivePermitLimitPerMinute", "2"));
        var (shopper, _) = await limited.SignedInCustomerAsync(_ct);
        var (other, _) = await limited.SignedInCustomerAsync(_ct);
        Task<HttpResponseMessage> TopUp(HttpClient c) =>
            c.PostAsJsonAsync("/api/wallet/top-ups", new TopUpRequest(10m, Guid.NewGuid().ToString()), _ct);

        Assert.Equal(HttpStatusCode.OK, (await TopUp(shopper)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await TopUp(shopper)).StatusCode);
        var blocked = await TopUp(shopper);

        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        Assert.InRange(blocked.Headers.RetryAfter!.Delta!.Value.TotalSeconds, 1, 60);
        var problem = await blocked.Content.ReadFromJsonAsync<JsonElement>(_ct);
        Assert.StartsWith("You're doing that too often", problem.GetProperty("detail").GetString(), StringComparison.Ordinal);
        // Another customer has their own allowance.
        Assert.Equal(HttpStatusCode.OK, (await TopUp(other)).StatusCode);
    }

    [Fact]
    public async Task All_api_traffic_has_a_per_ip_ceiling_but_health_checks_are_never_throttled()
    {
        await using var limited = factory.WithWebHostBuilder(b => b.UseSetting("RateLimiting:GlobalPermitLimitPerMinute", "3"));
        using var client = limited.CreateClient();

        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/catalog/categories", _ct)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.GetAsync("/api/catalog/categories", _ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/health/live", _ct)).StatusCode);
    }

    [Theory]
    [InlineData(true, HttpStatusCode.Unauthorized)] // trusted proxy: a different client IP gets its own allowance
    [InlineData(false, HttpStatusCode.TooManyRequests)] // untrusted: X-Forwarded-For is ignored, same bucket
    public async Task Client_ips_come_from_forwarded_headers_only_when_the_proxy_is_trusted(bool trusted, HttpStatusCode secondIp)
    {
        await using var limited = factory.WithWebHostBuilder(b => b
            .UseSetting("RateLimiting:AuthPermitLimitPerMinute", "1")
            .UseSetting("ForwardedHeaders:TrustAllProxies", trusted.ToString())
            .ConfigureServices(s => s.AddSingleton<IStartupFilter>(new DirectConnectionFrom(IPAddress.Parse("192.0.2.1")))));
        using var client = limited.CreateClient();
        Task<HttpResponseMessage> LoginFrom(string ip)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
            {
                Content = JsonContent.Create(new LoginRequest("nobody@quickkirana.test", "wrong-password")),
            };
            request.Headers.Add("X-Forwarded-For", ip);
            return client.SendAsync(request, _ct);
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginFrom("203.0.113.10")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await LoginFrom("203.0.113.10")).StatusCode);
        Assert.Equal(secondIp, (await LoginFrom("198.51.100.20")).StatusCode);
    }

    /// <summary>
    /// The in-memory test server has no client address at all, which forwarded-headers handling treats as
    /// trusted. This gives each request a real (public, non-proxy) peer address, as Kestrel would.
    /// </summary>
    private sealed class DirectConnectionFrom(IPAddress peer) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                context.Connection.RemoteIpAddress = peer;
                return nextMiddleware(context);
            });
            next(app);
        };
    }

    // ---------- Cookie keys ----------

    [Fact]
    public async Task Sign_ins_survive_a_restart_because_cookie_keys_live_in_the_database()
    {
        var (client, _) = await factory.SignedInCustomerAsync(_ct);
        var cookie = await CookieFromAsync(client);

        // A brand-new host (as after a redeploy) loads its key ring from the database.
        await using var restarted = factory.WithWebHostBuilder(_ => { });
        using var after = restarted.CreateClient(new() { HandleCookies = false });
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Add("Cookie", cookie);

        Assert.Equal(HttpStatusCode.OK, (await after.SendAsync(request, _ct)).StatusCode);
        await using var db = factory.CreateDbContext();
        Assert.True(await db.DataProtectionKeys.AnyAsync(_ct));
    }

    /// <summary>Signs in again on a cookie-less client to capture the Set-Cookie header.</summary>
    private async Task<string> CookieFromAsync(HttpClient signedIn)
    {
        var me = await signedIn.GetFromJsonAsync<CurrentUserResponse>("/api/auth/me", _ct);
        await using var db = factory.CreateDbContext();
        var phone = await db.Users.Where(u => u.Id == me!.Id).Select(u => u.PhoneNumber).SingleAsync(_ct);
        using var raw = factory.CreateClient(new() { HandleCookies = false });
        var login = await raw.PostAsJsonAsync("/api/auth/login", new LoginRequest(phone!, TestUsers.Password), _ct);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return login.Headers.GetValues("Set-Cookie").First(c => c.StartsWith("qk_auth=", StringComparison.Ordinal)).Split(';')[0];
    }

    // ---------- Start-up checks ----------

    [Fact]
    public void Production_refuses_to_start_with_a_short_admin_password()
    {
        using var production = factory.WithWebHostBuilder(b => b
            .UseEnvironment("Production")
            .UseSetting("AdminSeed:Password", "short-pw"));

        var error = Assert.Throws<OptionsValidationException>(() => production.CreateClient());
        Assert.Contains("at least 12 characters", error.Message, StringComparison.Ordinal);
    }
}

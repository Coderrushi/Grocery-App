using System.Net;
using GroceryStore.Api.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;

namespace GroceryStore.Api.Common.Security;

/// <summary>Which reverse proxies may tell the API the real client IP and scheme ("ForwardedHeaders" in appsettings).</summary>
public sealed class ForwardedHeadersSettings
{
    public const string SectionName = "ForwardedHeaders";

    /// <summary>Proxy IPs, e.g. a load balancer's private address.</summary>
    public string[] KnownProxies { get; set; } = [];

    /// <summary>Proxy networks in CIDR form, e.g. "10.0.0.0/8".</summary>
    public string[] KnownNetworks { get; set; } = [];

    /// <summary>
    /// Trust X-Forwarded-For from any address. Only safe when the app cannot be reached except through
    /// the platform's proxy (e.g. Render, where the container has no public port). Off by default.
    /// </summary>
    public bool TrustAllProxies { get; set; }
}

/// <summary>Hosting concerns that security depends on: proxies, cookie keys, request size.</summary>
internal static class HostingSecurity
{
    /// <summary>JSON bodies here are small; image uploads raise their own limit with [RequestSizeLimit].</summary>
    public const long MaxRequestBodyBytes = 256 * 1024;

    public static WebApplicationBuilder AddHostingSecurity(this WebApplicationBuilder builder)
    {
        builder.WebHost.ConfigureKestrel(kestrel => kestrel.Limits.MaxRequestBodySize = MaxRequestBodyBytes);

        // One application name keeps cookies valid across instances and deployments that share the key table.
        builder.Services.AddDataProtection()
            .SetApplicationName("QuickKirana")
            .PersistKeysToDbContext<AppDbContext>();

        var settings = builder.Configuration.GetSection(ForwardedHeadersSettings.SectionName).Get<ForwardedHeadersSettings>()
            ?? new ForwardedHeadersSettings();
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1; // only the nearest proxy's entry: anything earlier in the chain is client-controlled
            if (settings.TrustAllProxies)
            {
                options.KnownIPNetworks.Clear();
                options.KnownProxies.Clear();
            }

            foreach (var proxy in settings.KnownProxies)
            {
                options.KnownProxies.Add(IPAddress.Parse(proxy));
            }

            foreach (var network in settings.KnownNetworks)
            {
                options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
            }
        });

        return builder;
    }
}

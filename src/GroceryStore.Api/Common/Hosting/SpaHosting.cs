using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using GroceryStore.Api.Common.Media;
using Microsoft.Extensions.FileProviders;
using Microsoft.Net.Http.Headers;

namespace GroceryStore.Api.Common.Hosting;

/// <summary>Where the built React app lives ("Spa" in appsettings). The Docker image copies web/dist here.</summary>
public sealed class SpaOptions
{
    public const string SectionName = "Spa";

    public string RootPath { get; set; } = "wwwroot";
}

/// <summary>The web app as served by the API: its folder and the Content-Security-Policy for its pages.</summary>
public sealed record SpaSite(string RootPath, string ContentSecurityPolicy);

/// <summary>
/// Production serves the React app from the API (Phase 9), so the site and the API share one origin and
/// the SameSite=Strict auth cookie just works (ADR 0002). Hashed assets are cached for a year; index.html
/// is revalidated on every visit so a deploy reaches users immediately. Any non-API path without a file
/// returns index.html, and React Router takes over. In development Vite serves the app instead, and
/// this does nothing because there is no built index.html.
/// </summary>
internal static partial class SpaHosting
{
    private sealed class FallbackMarker;

    /// <summary>The built app, or null when there is none (development, tests).</summary>
    public static SpaSite? Find(WebApplication app)
    {
        var options = app.Configuration.GetSection(SpaOptions.SectionName).Get<SpaOptions>() ?? new SpaOptions();
        var root = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, options.RootPath));
        var index = Path.Combine(root, "index.html");
        if (!File.Exists(index))
        {
            return null;
        }

        string[] imageOrigins = app.Services.GetService<CloudinaryAccount>() is null ? [] : ["https://res.cloudinary.com"];
        return new SpaSite(root, ContentSecurityPolicyFor(File.ReadAllText(index), imageOrigins));
    }

    /// <summary>
    /// A strict policy for the app's pages: only our own scripts and styles. The one inline script in
    /// index.html (it applies the dark theme before the first paint) is allowed by its SHA-256 hash,
    /// computed from the built file itself, so the policy can never drift from what was deployed.
    /// React's style={…} props are set through the DOM, which style-src does not restrict.
    /// </summary>
    public static string ContentSecurityPolicyFor(string indexHtml, IEnumerable<string> imageOrigins)
    {
        var hashes = InlineScript().Matches(indexHtml)
            .Select(m => $"'sha256-{Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(m.Groups["body"].Value)))}'");
        return string.Join("; ",
            "default-src 'self'",
            string.Join(' ', ["script-src 'self'", .. hashes]),
            "style-src 'self'",
            string.Join(' ', ["img-src 'self' data:", .. imageOrigins]),
            "font-src 'self'",
            "connect-src 'self'",
            "object-src 'none'",
            "base-uri 'self'",
            "form-action 'self'",
            "frame-ancestors 'none'");
    }

    public static WebApplication UseSpa(this WebApplication app, SpaSite site)
    {
        var files = new PhysicalFileProvider(site.RootPath);
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = files,
            OnPrepareResponse = context => context.Context.Response.Headers[HeaderNames.CacheControl] =
                // Vite puts a content hash in every file name under /assets: a changed file has a new URL.
                context.Context.Request.Path.StartsWithSegments("/assets") ? "public, max-age=31536000, immutable" : "no-cache",
        });

        app.MapFallbackToFile("index.html", new StaticFileOptions
        {
            FileProvider = files,
            OnPrepareResponse = context => context.Context.Response.Headers[HeaderNames.CacheControl] = "no-cache",
        })
        .AllowAnonymous()
        .WithMetadata(new FallbackMarker());

        return app;
    }

    /// <summary>
    /// True when no real endpoint matched: nothing at all, or only the web-app fallback for a path
    /// that belongs to the API or media (an unknown /api URL is a 404, never the React app).
    /// </summary>
    public static bool IsUnmatched(HttpContext context) =>
        context.GetEndpoint() is not { } endpoint
        || (endpoint.Metadata.GetMetadata<FallbackMarker>() is not null
            && (context.Request.Path.StartsWithSegments("/api") || context.Request.Path.StartsWithSegments(MediaOptions.RequestPath)));

    [GeneratedRegex(@"<script(?![^>]*\bsrc=)[^>]*>(?<body>[\s\S]*?)</script>", RegexOptions.IgnoreCase)]
    private static partial Regex InlineScript();
}

namespace GroceryStore.Api.Common.Security;

/// <summary>
/// Browser-side defences sent with every response (ADR 0004). API responses are data, never pages, so
/// they get the strictest Content-Security-Policy: nothing may load, run or frame them.
/// (The web app's own HTML gets a page-appropriate policy when the API serves it in production.)
/// Headers are added as the response starts, so error responses — whose headers the exception
/// handler clears and rewrites — carry them too.
/// </summary>
internal static class SecurityHeaders
{
    public const string ApiContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use((context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers.XContentTypeOptions = "nosniff"; // never guess a type: a JSON body can't be run as a script
                headers.XFrameOptions = "DENY"; // no clickjacking (older browsers; CSP frame-ancestors for the rest)
                headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
                headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=(), usb=()";
                headers["Cross-Origin-Opener-Policy"] = "same-origin";

                var path = context.Request.Path;
                if (path.StartsWithSegments("/api") || path.StartsWithSegments("/media"))
                {
                    headers.ContentSecurityPolicy = ApiContentSecurityPolicy;
                }

                // Personal data (orders, wallet, addresses) must never sit in a shared or proxy cache.
                // An endpoint that chose its own caching keeps it.
                if (path.StartsWithSegments("/api") && !headers.ContainsKey("Cache-Control"))
                {
                    headers.CacheControl = "no-store";
                }

                return Task.CompletedTask;
            });
            return next(context);
        });
}

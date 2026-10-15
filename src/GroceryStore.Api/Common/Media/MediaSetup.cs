using Microsoft.Extensions.FileProviders;
using Microsoft.Net.Http.Headers;

namespace GroceryStore.Api.Common.Media;

internal static class MediaSetup
{
    public static IServiceCollection AddMedia(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var options = configuration.GetSection(MediaOptions.SectionName).Get<MediaOptions>() ?? new MediaOptions();
        var root = Path.GetFullPath(Path.Combine(environment.ContentRootPath, options.RootPath));
        Directory.CreateDirectory(root);
        return services.AddSingleton<IMediaStorage>(new LocalMediaStorage(root));
    }

    /// <summary>Serves uploaded files from /media with safe, long-lived caching headers.</summary>
    public static IApplicationBuilder UseMedia(this WebApplication app)
    {
        var storage = (LocalMediaStorage)app.Services.GetRequiredService<IMediaStorage>();
        return app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(storage.RootPath),
            RequestPath = MediaOptions.RequestPath,
            ServeUnknownFileTypes = false,
            OnPrepareResponse = context =>
            {
                var headers = context.Context.Response.Headers;
                // File names are random and never reused, so browsers may cache them for a year.
                headers[HeaderNames.CacheControl] = "public, max-age=31536000, immutable";
                // Stop browsers from "sniffing" a file into a more dangerous type (e.g. HTML).
                headers[HeaderNames.XContentTypeOptions] = "nosniff";
            },
        });
    }
}

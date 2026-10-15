namespace GroceryStore.Api.Common.Media;

public sealed class MediaOptions
{
    public const string SectionName = "Media";
    public const string RequestPath = "/media";

    /// <summary>Folder for uploaded files. Relative paths are resolved against the app's content root.</summary>
    public string RootPath { get; set; } = "media";
}

/// <summary>
/// Where uploaded files live. Today: the local disk, served by the API under /media. Moving to cloud
/// object storage later means writing one new implementation of this interface.
/// </summary>
public interface IMediaStorage
{
    /// <summary>Saves the stream under a new random name and returns its relative path, e.g. "products/ab12….webp".</summary>
    Task<string> SaveAsync(Stream content, string folder, string extension, CancellationToken ct);

    Task DeleteAsync(string relativePath, CancellationToken ct);

    /// <summary>The URL browsers use to fetch a stored file.</summary>
    string PublicUrl(string relativePath);
}

internal sealed class LocalMediaStorage(string rootPath) : IMediaStorage
{
    public string RootPath { get; } = rootPath;

    public async Task<string> SaveAsync(Stream content, string folder, string extension, CancellationToken ct)
    {
        // Random names: no user-controlled path segments (no path traversal), no collisions, and files
        // can be cached forever because a changed image always gets a new URL.
        var relativePath = $"{folder}/{Guid.NewGuid():N}{extension}";
        var fullPath = FullPath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var file = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write);
        await content.CopyToAsync(file, ct);
        return relativePath;
    }

    public Task DeleteAsync(string relativePath, CancellationToken ct)
    {
        File.Delete(FullPath(relativePath)); // no-op if already gone
        return Task.CompletedTask;
    }

    public string PublicUrl(string relativePath) => $"{MediaOptions.RequestPath}/{relativePath}";

    private string FullPath(string relativePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(RootPath, relativePath));
        if (!fullPath.StartsWith(Path.GetFullPath(RootPath), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Media path escapes the media root.");
        }

        return fullPath;
    }
}

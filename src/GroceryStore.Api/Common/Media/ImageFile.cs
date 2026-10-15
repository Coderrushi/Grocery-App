namespace GroceryStore.Api.Common.Media;

/// <summary>
/// Identifies an uploaded image by its first bytes ("magic numbers"), never by its file name or the
/// browser-supplied content type, both of which an attacker controls.
/// </summary>
public static class ImageFile
{
    public const long MaxBytes = 2 * 1024 * 1024;

    /// <summary>Returns ".jpg", ".png" or ".webp", or null if the bytes are not one of those formats.</summary>
    public static string? DetectExtension(ReadOnlySpan<byte> header)
    {
        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            return ".jpg";
        }

        if (header.Length >= 8 && header[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
        {
            return ".png";
        }

        if (header.Length >= 12 && header[..4].SequenceEqual("RIFF"u8) && header[8..12].SequenceEqual("WEBP"u8))
        {
            return ".webp";
        }

        return null;
    }
}

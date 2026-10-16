using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;

namespace GroceryStore.Api.Common.Media;

/// <summary>Cloudinary account details, parsed from the CLOUDINARY_URL shown on the Cloudinary dashboard.</summary>
public sealed record CloudinaryAccount(string CloudName, string ApiKey, string ApiSecret)
{
    /// <summary>Parses "cloudinary://API_KEY:API_SECRET@CLOUD_NAME".</summary>
    public static CloudinaryAccount Parse(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "cloudinary"
            || uri.UserInfo.Split(':') is not [var key, var secret] || key.Length == 0 || secret.Length == 0 || uri.Host.Length == 0)
        {
            throw new InvalidOperationException("Media:CloudinaryUrl must look like cloudinary://API_KEY:API_SECRET@CLOUD_NAME.");
        }

        return new CloudinaryAccount(uri.Host, Uri.UnescapeDataString(key), Uri.UnescapeDataString(secret));
    }
}

/// <summary>
/// Product photos in Cloudinary (Phase 9): the hosting platform's disk is wiped on every restart, so
/// files must live elsewhere. Uses Cloudinary's signed REST upload API directly — two calls, no SDK.
/// The stored path is the Cloudinary public id ("products/3f2a…"); browsers load images straight from
/// Cloudinary's CDN, resized and converted to WebP/AVIF on the fly.
/// </summary>
internal sealed class CloudinaryMediaStorage(HttpClient http, CloudinaryAccount account, TimeProvider clock) : IMediaStorage
{
    /// <summary>Delivery transformation: at most 800px wide, best format and quality for the browser.</summary>
    public const string DeliveryTransformation = "c_limit,w_800,f_auto,q_auto";

    private string ApiBase => $"https://api.cloudinary.com/v1_1/{account.CloudName}/image";

    public async Task<string> SaveAsync(Stream content, string folder, string extension, CancellationToken ct)
    {
        var parameters = Signed(new() { ["public_id"] = $"{folder}/{Guid.NewGuid():N}" });
        using var form = new MultipartFormDataContent();
        foreach (var (name, value) in parameters)
        {
            form.Add(new StringContent(value), name);
        }

        form.Add(new StreamContent(content), "file", $"upload{extension}");

        using var response = await http.PostAsync($"{ApiBase}/upload", form, ct);
        await EnsureSuccessAsync(response, "upload", ct);
        var result = await response.Content.ReadFromJsonAsync<UploadResult>(ct);
        return result?.public_id ?? throw new InvalidOperationException("Cloudinary upload returned no public id.");
    }

    public async Task DeleteAsync(string relativePath, CancellationToken ct)
    {
        // invalidate=true also purges the CDN copy, so a removed photo stops being served.
        using var form = new FormUrlEncodedContent(Signed(new() { ["public_id"] = relativePath, ["invalidate"] = "true" }));
        using var response = await http.PostAsync($"{ApiBase}/destroy", form, ct);
        await EnsureSuccessAsync(response, "delete", ct); // "not found" is a 200 with result=not found: fine
    }

    public string PublicUrl(string relativePath) =>
        $"https://res.cloudinary.com/{account.CloudName}/image/upload/{DeliveryTransformation}/{relativePath}";

    /// <summary>
    /// Adds timestamp, api_key and signature: SHA-1 of the alphabetically sorted "name=value" pairs
    /// joined by "&amp;", followed by the API secret (Cloudinary's documented signing scheme).
    /// </summary>
    internal Dictionary<string, string> Signed(SortedDictionary<string, string> parameters)
    {
        parameters["timestamp"] = clock.GetUtcNow().ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var toSign = string.Join('&', parameters.Select(p => $"{p.Key}={p.Value}")) + account.ApiSecret;
#pragma warning disable CA5350 // SHA-1 is what Cloudinary's API signature requires; it authenticates a request, it isn't protecting data at rest.
        var signature = Convert.ToHexStringLower(SHA1.HashData(Encoding.UTF8.GetBytes(toSign)));
#pragma warning restore CA5350
        return new Dictionary<string, string>(parameters) { ["api_key"] = account.ApiKey, ["signature"] = signature };
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string action, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            // The body names the problem (bad credentials, file too large); it never contains our secret.
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Cloudinary {action} failed ({(int)response.StatusCode}): {body}");
        }
    }

#pragma warning disable IDE1006, CA1812 // JSON shape of Cloudinary's response; instantiated by the deserializer.
    private sealed record UploadResult(string public_id);
#pragma warning restore IDE1006, CA1812
}

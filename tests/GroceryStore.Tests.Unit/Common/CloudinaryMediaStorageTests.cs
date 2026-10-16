using System.Net;
using GroceryStore.Api.Common.Media;
using GroceryStore.Api.Data;
using MySql.Data.MySqlClient;

namespace GroceryStore.Tests.Unit.Common;

public sealed class CloudinaryMediaStorageTests
{
    private sealed class FixedClock(long unixSeconds) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
    }

    /// <summary>Records each request and answers with a canned response.</summary>
    private sealed class FakeCloudinary(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public List<(Uri Url, string Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add((request.RequestUri!, await request.Content!.ReadAsStringAsync(ct)));
            return new HttpResponseMessage(status) { Content = new StringContent(body) };
        }
    }

    private static readonly CloudinaryAccount Account = new("demo-shop", "123456", "abcd");

    private static CloudinaryMediaStorage Storage(FakeCloudinary handler, long now = 1315060510) =>
        new(new HttpClient(handler), Account, new FixedClock(now));

    [Fact]
    public void Signs_requests_exactly_as_cloudinarys_documentation_example()
    {
        // From Cloudinary's "Generating authentication signatures" guide: secret "abcd", timestamp 1315060510.
        var signed = Storage(new FakeCloudinary(HttpStatusCode.OK, "{}")).Signed(new()
        {
            ["eager"] = "w_400,h_300,c_pad|w_260,h_200,c_crop",
            ["public_id"] = "sample_image",
        });

        Assert.Equal("bfd09f95f331f558cbd1320e67aa8d488770583e", signed["signature"]);
        Assert.Equal(("1315060510", "123456"), (signed["timestamp"], signed["api_key"]));
    }

    [Fact]
    public async Task Uploads_to_the_account_under_a_random_name_in_the_folder_and_keeps_the_public_id()
    {
        var handler = new FakeCloudinary(HttpStatusCode.OK, """{"public_id":"products/abc123","secure_url":"https://x"}""");

        var path = await Storage(handler).SaveAsync(new MemoryStream([1, 2, 3]), "products", ".webp", CancellationToken.None);

        Assert.Equal("products/abc123", path);
        var (url, body) = Assert.Single(handler.Requests);
        Assert.Equal("https://api.cloudinary.com/v1_1/demo-shop/image/upload", url.ToString());
        Assert.Matches("name=public_id\\s+products/[0-9a-f]{32}", body);
        Assert.Contains("name=signature", body, StringComparison.Ordinal);
        Assert.DoesNotContain("abcd", body, StringComparison.Ordinal); // the secret signs, it is never sent
    }

    [Fact]
    public async Task Deleting_also_purges_the_cdn_copy()
    {
        var handler = new FakeCloudinary(HttpStatusCode.OK, """{"result":"ok"}""");

        await Storage(handler).DeleteAsync("products/abc123", CancellationToken.None);

        var (url, body) = Assert.Single(handler.Requests);
        Assert.Equal("https://api.cloudinary.com/v1_1/demo-shop/image/destroy", url.ToString());
        Assert.Contains("invalidate=true", body, StringComparison.Ordinal);
        Assert.Contains("public_id=products%2Fabc123", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_refused_upload_fails_loudly_with_cloudinarys_reason()
    {
        var handler = new FakeCloudinary(HttpStatusCode.Unauthorized, """{"error":{"message":"Invalid Signature"}}""");

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Storage(handler).SaveAsync(new MemoryStream([1]), "products", ".jpg", CancellationToken.None));

        Assert.Contains("401", error.Message, StringComparison.Ordinal);
        Assert.Contains("Invalid Signature", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Images_are_served_from_the_cdn_resized_and_in_the_best_format() =>
        Assert.Equal(
            "https://res.cloudinary.com/demo-shop/image/upload/c_limit,w_800,f_auto,q_auto/products/abc123",
            Storage(new FakeCloudinary(HttpStatusCode.OK, "{}")).PublicUrl("products/abc123"));

    [Theory]
    [InlineData("cloudinary://123456:abcd@demo-shop", true)]
    [InlineData("https://123456:abcd@demo-shop", false)]
    [InlineData("cloudinary://123456@demo-shop", false)]
    [InlineData("not a url", false)]
    public void The_dashboard_url_is_parsed_or_rejected_at_start_up(string url, bool valid)
    {
        if (valid)
        {
            Assert.Equal(Account, CloudinaryAccount.Parse(url));
        }
        else
        {
            Assert.Throws<InvalidOperationException>(() => CloudinaryAccount.Parse(url));
        }
    }
}

public sealed class DatabaseTlsTests
{
    [Fact]
    public void A_configured_ca_certificate_turns_on_server_certificate_verification()
    {
        var result = DataServiceExtensions.WithVerifiedTls(
            "Server=db.example;Database=shop;User=app;Password=pw;SslMode=Required",
            "-----BEGIN CERTIFICATE-----\\nMIIB\\n-----END CERTIFICATE-----");

        var parsed = new MySqlConnectionStringBuilder(result);
        Assert.Equal(MySqlSslMode.VerifyCA, parsed.SslMode);
        Assert.Equal("-----BEGIN CERTIFICATE-----\nMIIB\n-----END CERTIFICATE-----", File.ReadAllText(parsed.SslCa));
    }

    [Fact]
    public void Without_a_certificate_the_connection_string_is_unchanged() =>
        Assert.Equal("Server=x;SslMode=Required", DataServiceExtensions.WithVerifiedTls("Server=x;SslMode=Required", null));
}

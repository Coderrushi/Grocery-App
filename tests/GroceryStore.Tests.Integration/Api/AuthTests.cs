using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GroceryStore.Api.Modules.Identity;
using GroceryStore.Tests.Integration.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace GroceryStore.Tests.Integration.Api;

public sealed class AuthTests(GroceryApiFactory factory)
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Register_signs_the_customer_in_with_a_secure_cookie()
    {
        var customer = TestUsers.NewCustomer();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", customer, _ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith("qk_auth=", StringComparison.Ordinal));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);

        var me = await client.GetFromJsonAsync<CurrentUserResponse>("/api/auth/me", _ct);
        Assert.NotNull(me);
        Assert.Equal(customer.Email, me.Email);
        Assert.Equal([Roles.Customer], me.Roles);
    }

    [Fact]
    public async Task Register_reports_errors_per_field_using_json_names()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("", "not-an-email", "12345", "short"), _ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>(_ct)).GetProperty("errors");
        Assert.True(errors.TryGetProperty("fullName", out _));
        Assert.True(errors.TryGetProperty("email", out _));
        Assert.Equal("Enter a valid 10-digit mobile number.", errors.GetProperty("phoneNumber")[0].GetString());
        Assert.True(errors.TryGetProperty("password", out _));
    }

    [Fact]
    public async Task Register_rejects_weak_password_with_identity_rule_on_the_password_field()
    {
        using var client = factory.CreateClient();
        var customer = TestUsers.NewCustomer() with { Password = "aaaaaaaa" }; // 8 chars, 1 unique

        var response = await client.PostAsJsonAsync("/api/auth/register", customer, _ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>(_ct)).GetProperty("errors");
        Assert.True(errors.TryGetProperty("password", out _));
    }

    [Fact]
    public async Task Register_rejects_duplicate_email_and_duplicate_mobile()
    {
        var (_, existing) = await factory.SignedInCustomerAsync(_ct);
        using var client = factory.CreateClient();

        var sameEmail = await client.PostAsJsonAsync("/api/auth/register",
            TestUsers.NewCustomer() with { Email = existing.Email.ToUpperInvariant() }, _ct);
        var samePhone = await client.PostAsJsonAsync("/api/auth/register",
            TestUsers.NewCustomer() with { PhoneNumber = existing.PhoneNumber }, _ct);

        Assert.Equal(HttpStatusCode.Conflict, sameEmail.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, samePhone.StatusCode);
    }

    [Theory]
    [InlineData("email")]
    [InlineData("mobile")]
    [InlineData("formatted mobile")]
    public async Task Login_accepts_email_or_mobile_number(string identifierKind)
    {
        var customer = TestUsers.NewCustomer();
        using (var registration = factory.CreateClient())
        {
            await registration.PostAsJsonAsync("/api/auth/register", customer, _ct);
        }

        var identifier = identifierKind switch
        {
            "email" => customer.Email,
            "mobile" => customer.PhoneNumber,
            _ => $"+91 {customer.PhoneNumber[..5]} {customer.PhoneNumber[5..]}",
        };
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(identifier, customer.Password), _ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me", _ct)).StatusCode);
    }

    [Fact]
    public async Task Wrong_password_and_unknown_user_get_the_same_message()
    {
        var (_, customer) = await factory.SignedInCustomerAsync(_ct);
        using var client = factory.CreateClient();

        var wrongPassword = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(customer.Email, "wrong-password"), _ct);
        var unknownUser = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("nobody@quickkirana.test", "wrong-password"), _ct);

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownUser.StatusCode);
        Assert.Equal(
            (await wrongPassword.Content.ReadFromJsonAsync<JsonElement>(_ct)).GetProperty("detail").GetString(),
            (await unknownUser.Content.ReadFromJsonAsync<JsonElement>(_ct)).GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Account_locks_after_five_failed_attempts()
    {
        var (_, customer) = await factory.SignedInCustomerAsync(_ct);
        using var client = factory.CreateClient();

        for (var i = 0; i < 5; i++)
        {
            await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(customer.Email, "wrong-password"), _ct);
        }

        var withCorrectPassword = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(customer.Email, customer.Password), _ct);

        Assert.Equal(HttpStatusCode.Unauthorized, withCorrectPassword.StatusCode);
        var detail = (await withCorrectPassword.Content.ReadFromJsonAsync<JsonElement>(_ct)).GetProperty("detail").GetString();
        Assert.Contains("locked", detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Logout_ends_the_session()
    {
        var (client, _) = await factory.SignedInCustomerAsync(_ct);

        var logout = await client.PostAsync("/api/auth/logout", null, _ct);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.GetAsync("/api/auth/me", _ct)).StatusCode); // a visitor again
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/orders", _ct)).StatusCode);
    }

    [Fact]
    public async Task Asking_who_is_signed_in_is_not_an_error_for_a_visitor()
    {
        using var visitor = factory.CreateClient();

        var response = await visitor.GetAsync("/api/auth/me", _ct);

        // 204, not 401: the web app asks on every page load, and a 401 shows as a red console error.
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync(_ct));
    }

    [Fact]
    public async Task Anonymous_request_to_protected_endpoint_gets_401_problem_details_not_a_redirect()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/api/orders", _ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Change_password_validates_the_current_password_then_takes_effect()
    {
        var (client, customer) = await factory.SignedInCustomerAsync(_ct);

        var wrong = await client.PostAsJsonAsync("/api/account/change-password",
            new ChangePasswordRequest("not-my-password", "brand-new-password"), _ct);
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        Assert.True((await wrong.Content.ReadFromJsonAsync<JsonElement>(_ct)).GetProperty("errors").TryGetProperty("currentPassword", out _));

        var ok = await client.PostAsJsonAsync("/api/account/change-password",
            new ChangePasswordRequest(customer.Password, "brand-new-password"), _ct);
        Assert.Equal(HttpStatusCode.NoContent, ok.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me", _ct)).StatusCode); // still signed in

        using var fresh = factory.CreateClient();
        var login = await fresh.PostAsJsonAsync("/api/auth/login", new LoginRequest(customer.Email, "brand-new-password"), _ct);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task Profile_update_rejects_a_mobile_number_owned_by_someone_else()
    {
        var (_, other) = await factory.SignedInCustomerAsync(_ct);
        var (client, _) = await factory.SignedInCustomerAsync(_ct);

        var taken = await client.PutAsJsonAsync("/api/account/profile", new UpdateProfileRequest("Me", other.PhoneNumber), _ct);
        var renamed = await client.PutAsJsonAsync("/api/account/profile", new UpdateProfileRequest("Asha Verma", "8123456780"), _ct);

        Assert.Equal(HttpStatusCode.Conflict, taken.StatusCode);
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        Assert.Equal("Asha Verma", (await renamed.Content.ReadFromJsonAsync<CurrentUserResponse>(_ct))!.FullName);
    }

    [Fact]
    public async Task Auth_endpoints_are_rate_limited()
    {
        await using var limited = factory.WithWebHostBuilder(b => b.UseSetting("RateLimiting:AuthPermitLimitPerMinute", "3"));
        using var client = limited.CreateClient();
        var attempt = new LoginRequest("nobody@quickkirana.test", "whatever-password");

        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", attempt, _ct)).StatusCode);
        }

        var blocked = await client.PostAsJsonAsync("/api/auth/login", attempt, _ct);
        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
    }

    [Fact]
    public async Task Admin_customer_list_requires_the_admin_role()
    {
        var (customerClient, customer) = await factory.SignedInCustomerAsync(_ct);
        using var anonymous = factory.CreateClient();
        using var admin = await factory.SignedInAdminAsync(_ct);
        var url = $"/api/admin/customers?search={Uri.EscapeDataString(customer.PhoneNumber)}";

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(url, _ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await customerClient.GetAsync(url, _ct)).StatusCode);

        var page = await admin.GetFromJsonAsync<JsonElement>(url, _ct);
        Assert.Equal(1, page.GetProperty("totalCount").GetInt32());
        Assert.Equal(customer.Email, page.GetProperty("items")[0].GetProperty("email").GetString());
    }
}

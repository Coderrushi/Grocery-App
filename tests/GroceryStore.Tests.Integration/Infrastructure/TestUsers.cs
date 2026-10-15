using System.Net;
using System.Net.Http.Json;
using GroceryStore.Api.Modules.Identity;

namespace GroceryStore.Tests.Integration.Infrastructure;

internal static class TestUsers
{
    public const string Password = "correct-horse-battery";

    private static int _counter = Random.Shared.Next(0, 10_000_000);

    /// <summary>A unique, valid, not-yet-registered customer.</summary>
    public static RegisterRequest NewCustomer()
    {
        var n = Interlocked.Increment(ref _counter) % 100_000_000;
        return new RegisterRequest(
            FullName: $"Test Customer {n}",
            Email: $"customer{n}.{Guid.NewGuid():N}@quickkirana.test",
            PhoneNumber: $"9{n:D9}",
            Password: Password);
    }

    /// <summary>Registers a new customer and returns a client carrying their auth cookie.</summary>
    public static async Task<(HttpClient Client, RegisterRequest Customer)> SignedInCustomerAsync(
        this GroceryApiFactory factory, CancellationToken ct)
    {
        var customer = NewCustomer();
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", customer, ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (client, customer);
    }

    public static async Task<HttpClient> SignedInAdminAsync(this GroceryApiFactory factory, CancellationToken ct)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(GroceryApiFactory.AdminEmail, GroceryApiFactory.AdminPassword),
            ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return client;
    }
}

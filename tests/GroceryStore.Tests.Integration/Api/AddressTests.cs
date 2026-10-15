using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GroceryStore.Api.Modules.Customers;
using GroceryStore.Tests.Integration.Infrastructure;

namespace GroceryStore.Tests.Integration.Api;

public sealed class AddressTests(GroceryApiFactory factory)
{
    private const string Url = "/api/account/addresses";
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private static SaveAddressRequest NewAddress(string line1 = "Flat 4B, Shanti Apartments", bool isDefault = false) =>
        new(AddressLabel.Home, "Ravi Kumar", "9876543210", line1, "MG Road, Kothrud", "Near City Pride",
            "Pune", "Maharashtra", "411038", isDefault);

    private async Task<AddressResponse> CreateAsync(HttpClient client, SaveAddressRequest request)
    {
        var response = await client.PostAsJsonAsync(Url, request, _ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AddressResponse>(_ct))!;
    }

    private async Task<List<AddressResponse>> ListAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<List<AddressResponse>>(Url, _ct))!;

    [Fact]
    public async Task First_address_becomes_the_default_automatically()
    {
        var (client, _) = await factory.SignedInCustomerAsync(_ct);

        var created = await CreateAsync(client, NewAddress(isDefault: false));

        Assert.True(created.IsDefault);
        Assert.Equal(AddressLabel.Home, created.Label);
    }

    [Fact]
    public async Task Making_a_new_address_default_clears_the_previous_default()
    {
        var (client, _) = await factory.SignedInCustomerAsync(_ct);
        var first = await CreateAsync(client, NewAddress("Home"));

        var second = await CreateAsync(client, NewAddress("Office", isDefault: true));

        var all = await ListAsync(client);
        Assert.Equal(second.Id, Assert.Single(all, a => a.IsDefault).Id);
        Assert.Equal(second.Id, all[0].Id); // default listed first
        Assert.Contains(all, a => a.Id == first.Id && !a.IsDefault);
    }

    [Fact]
    public async Task Address_fields_are_validated_with_indian_formats()
    {
        var (client, _) = await factory.SignedInCustomerAsync(_ct);

        var response = await client.PostAsJsonAsync(Url,
            NewAddress() with { Pincode = "011038", State = "Bombay", PhoneNumber = "5876543210", Line1 = "" }, _ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>(_ct)).GetProperty("errors");
        foreach (var field in new[] { "pincode", "state", "phoneNumber", "line1" })
        {
            Assert.True(errors.TryGetProperty(field, out _), $"expected an error for {field}");
        }
    }

    [Fact]
    public async Task Customers_cannot_see_or_change_each_others_addresses()
    {
        var (owner, _) = await factory.SignedInCustomerAsync(_ct);
        var (intruder, _) = await factory.SignedInCustomerAsync(_ct);
        var address = await CreateAsync(owner, NewAddress());

        Assert.Equal(HttpStatusCode.NotFound, (await intruder.GetAsync($"{Url}/{address.Id}", _ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.PutAsJsonAsync($"{Url}/{address.Id}", NewAddress("Hacked"), _ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.DeleteAsync($"{Url}/{address.Id}", _ct)).StatusCode);
        Assert.Empty(await ListAsync(intruder));
        Assert.Equal("Flat 4B, Shanti Apartments", (await ListAsync(owner)).Single().Line1);
    }

    [Fact]
    public async Task Update_changes_fields_and_cannot_remove_the_only_default()
    {
        var (client, _) = await factory.SignedInCustomerAsync(_ct);
        var address = await CreateAsync(client, NewAddress());

        var response = await client.PutAsJsonAsync($"{Url}/{address.Id}",
            NewAddress("House 12") with { Label = AddressLabel.Work, Landmark = "  ", IsDefault = false }, _ct);

        var updated = (await response.Content.ReadFromJsonAsync<AddressResponse>(_ct))!;
        Assert.Equal("House 12", updated.Line1);
        Assert.Equal(AddressLabel.Work, updated.Label);
        Assert.Null(updated.Landmark); // blank optional fields are stored as null
        Assert.True(updated.IsDefault);
    }

    [Fact]
    public async Task Deleting_the_default_promotes_another_address()
    {
        var (client, _) = await factory.SignedInCustomerAsync(_ct);
        var first = await CreateAsync(client, NewAddress("One"));
        var second = await CreateAsync(client, NewAddress("Two"));

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"{Url}/{first.Id}", _ct)).StatusCode);

        var remaining = Assert.Single(await ListAsync(client));
        Assert.Equal(second.Id, remaining.Id);
        Assert.True(remaining.IsDefault);
    }

    [Fact]
    public async Task A_customer_can_save_at_most_ten_addresses()
    {
        var (client, _) = await factory.SignedInCustomerAsync(_ct);
        for (var i = 0; i < Address.MaxPerCustomer; i++)
        {
            await CreateAsync(client, NewAddress($"House {i}"));
        }

        var eleventh = await client.PostAsJsonAsync(Url, NewAddress("One too many"), _ct);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, eleventh.StatusCode);
    }

    [Fact]
    public async Task Concurrent_set_default_requests_leave_exactly_one_default()
    {
        var (client, _) = await factory.SignedInCustomerAsync(_ct);
        var ids = new List<int>();
        for (var i = 0; i < 6; i++)
        {
            ids.Add((await CreateAsync(client, NewAddress($"House {i}"))).Id);
        }

        var responses = await Task.WhenAll(ids.Select(id => client.PostAsync($"{Url}/{id}/default", null, _ct)));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.NoContent, r.StatusCode));
        Assert.Single(await ListAsync(client), a => a.IsDefault);
    }
}

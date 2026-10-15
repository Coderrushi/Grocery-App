using GroceryStore.Api.Common.Errors;
using GroceryStore.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace GroceryStore.Api.Modules.Customers;

/// <summary>
/// A customer's saved addresses. Every query is scoped to the caller's user id, so one customer can
/// never read or change another's address. A foreign id simply returns 404.
/// Invariant: if a customer has any addresses, exactly one of them is the default.
/// </summary>
public sealed class AddressService(AppDbContext db, TimeProvider timeProvider)
{
    private IQueryable<Address> AddressesOf(int userId) => db.Set<Address>().Where(a => a.UserId == userId);

    public async Task<IReadOnlyList<AddressResponse>> ListAsync(int userId, CancellationToken ct) =>
        await AddressesOf(userId)
            .OrderByDescending(a => a.IsDefault).ThenByDescending(a => a.UpdatedAtUtc)
            .Select(a => AddressResponse.From(a))
            .ToListAsync(ct);

    public async Task<AddressResponse> GetAsync(int userId, int addressId, CancellationToken ct) =>
        AddressResponse.From(await FindAsync(userId, addressId, ct));

    public async Task<AddressResponse> CreateAsync(int userId, SaveAddressRequest request, CancellationToken ct)
    {
        var existing = await AddressesOf(userId).CountAsync(ct);
        if (existing >= Address.MaxPerCustomer)
        {
            throw new BusinessRuleException(
                $"You can save up to {Address.MaxPerCustomer} addresses. Delete one you no longer use to add another.");
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var address = new Address { UserId = userId, CreatedAtUtc = now };
        Apply(request, address, now); // IsDefault stays false here; set below, atomically with clearing the old default

        var makeDefault = request.IsDefault || existing == 0;

        await InTransactionAsync(async () =>
        {
            db.Add(address);
            await db.SaveChangesAsync(ct);
            if (makeDefault)
            {
                await SetDefaultCoreAsync(userId, address.Id, ct);
            }
        }, ct);

        address.IsDefault = makeDefault;
        return AddressResponse.From(address);
    }

    public async Task<AddressResponse> UpdateAsync(int userId, int addressId, SaveAddressRequest request, CancellationToken ct)
    {
        var address = await FindAsync(userId, addressId, ct);
        var wasDefault = address.IsDefault;
        Apply(request, address, timeProvider.GetUtcNow().UtcDateTime);

        // Un-ticking "default" on the default address is ignored: there must always be one default,
        // and the customer changes it by marking a different address as default.
        address.IsDefault = wasDefault;
        var makeDefault = request.IsDefault && !wasDefault;

        await InTransactionAsync(async () =>
        {
            await db.SaveChangesAsync(ct);
            if (makeDefault)
            {
                await SetDefaultCoreAsync(userId, address.Id, ct);
            }
        }, ct);

        address.IsDefault = wasDefault || makeDefault;
        return AddressResponse.From(address);
    }

    public async Task SetDefaultAsync(int userId, int addressId, CancellationToken ct)
    {
        await FindAsync(userId, addressId, ct); // 404 if not the caller's address
        await SetDefaultCoreAsync(userId, addressId, ct);
    }

    public async Task DeleteAsync(int userId, int addressId, CancellationToken ct)
    {
        var address = await FindAsync(userId, addressId, ct);

        await InTransactionAsync(async () =>
        {
            db.Remove(address);
            await db.SaveChangesAsync(ct);

            if (address.IsDefault)
            {
                // Promote the most recently used remaining address, if any.
                var nextId = await AddressesOf(userId)
                    .OrderByDescending(a => a.UpdatedAtUtc).Select(a => (int?)a.Id).FirstOrDefaultAsync(ct);
                if (nextId is { } id)
                {
                    await SetDefaultCoreAsync(userId, id, ct);
                }
            }
        }, ct);
    }

    /// <summary>
    /// One UPDATE statement marks the chosen address and clears all others. Being a single statement,
    /// it is atomic: concurrent requests cannot leave a customer with two default addresses.
    /// </summary>
    private Task<int> SetDefaultCoreAsync(int userId, int addressId, CancellationToken ct) =>
        AddressesOf(userId).ExecuteUpdateAsync(s => s.SetProperty(a => a.IsDefault, a => a.Id == addressId), ct);

    private async Task<Address> FindAsync(int userId, int addressId, CancellationToken ct) =>
        await AddressesOf(userId).SingleOrDefaultAsync(a => a.Id == addressId, ct)
        ?? throw new NotFoundException("Address not found.");

    private Task InTransactionAsync(Func<Task> work, CancellationToken ct) =>
        db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await work();
            await transaction.CommitAsync(ct);
        });

    private static void Apply(SaveAddressRequest request, Address address, DateTime now)
    {
        address.Label = request.Label;
        address.RecipientName = request.RecipientName.Trim();
        address.PhoneNumber = request.PhoneNumber;
        address.Line1 = request.Line1.Trim();
        address.Line2 = NullIfBlank(request.Line2);
        address.Landmark = NullIfBlank(request.Landmark);
        address.City = request.City.Trim();
        address.State = request.State;
        address.Pincode = request.Pincode;
        address.UpdatedAtUtc = now;
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

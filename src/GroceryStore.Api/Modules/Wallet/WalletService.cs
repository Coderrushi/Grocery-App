using GroceryStore.Api.Common;
using GroceryStore.Api.Common.Errors;
using GroceryStore.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace GroceryStore.Api.Modules.Wallet;

/// <summary>
/// The only code that moves money in or out of a wallet. Each movement is one atomic conditional UPDATE
/// of the balance plus one ledger row, in the caller's transaction. Like InventoryService, it can never
/// overdraw, even under concurrent requests, and the DB CHECK constraint is the final backstop.
/// </summary>
public sealed class WalletService(AppDbContext db, TimeProvider timeProvider)
{
    private IQueryable<Wallet> WalletOf(int userId) => db.Set<Wallet>().Where(w => w.UserId == userId);

    public async Task<decimal> GetBalanceAsync(int userId, CancellationToken ct) =>
        await WalletOf(userId).Select(w => (decimal?)w.Balance).SingleOrDefaultAsync(ct) ?? 0m;

    public Task<PagedResult<WalletTransactionResponse>> GetTransactionsAsync(int userId, PageRequest page, CancellationToken ct) =>
        db.Set<WalletTransaction>()
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAtUtc).ThenByDescending(t => t.Id)
            .Select(t => new WalletTransactionResponse(t.Id, t.Type, t.Amount, t.BalanceAfter, t.Description, t.CreatedAtUtc))
            .ToPagedResultAsync(page, ct);

    /// <summary>
    /// Adds money. Must run inside a transaction (the caller's). <paramref name="maxBalance"/> is enforced
    /// atomically when given (customer top-ups); refunds and admin credits pass null.
    /// </summary>
    internal async Task<decimal> CreditAsync(
        int userId, decimal amount, WalletTransactionType type, string description, string? reference,
        int? createdByUserId, decimal? maxBalance, CancellationToken ct)
    {
        EnsurePositive(amount);
        await EnsureWalletExistsAsync(userId, ct);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var wallets = WalletOf(userId);
        if (maxBalance is { } max)
        {
            wallets = wallets.Where(w => w.Balance + amount <= max);
        }

        var updated = await wallets.ExecuteUpdateAsync(s => s
            .SetProperty(w => w.Balance, w => w.Balance + amount)
            .SetProperty(w => w.UpdatedAtUtc, now), ct);

        if (updated == 0)
        {
            throw new BusinessRuleException(
                $"Your wallet can hold up to {Money.Format(maxBalance!.Value)}. " +
                $"You can add at most {Money.Format(Math.Max(0, maxBalance.Value - await GetBalanceAsync(userId, ct)))} right now.");
        }

        return await RecordAsync(userId, amount, type, description, reference, createdByUserId, now, ct);
    }

    /// <summary>Takes money out. Must run inside a transaction. Fails with 422 if the balance is too low.</summary>
    internal async Task<decimal> DebitAsync(
        int userId, decimal amount, WalletTransactionType type, string description, string? reference,
        int? createdByUserId, CancellationToken ct)
    {
        EnsurePositive(amount);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var updated = await WalletOf(userId)
            .Where(w => w.Balance >= amount)
            .ExecuteUpdateAsync(s => s
                .SetProperty(w => w.Balance, w => w.Balance - amount)
                .SetProperty(w => w.UpdatedAtUtc, now), ct);

        if (updated == 0)
        {
            throw new BusinessRuleException(
                $"Insufficient wallet balance: {Money.Format(await GetBalanceAsync(userId, ct))} available, {Money.Format(amount)} needed.");
        }

        return await RecordAsync(userId, -amount, type, description, reference, createdByUserId, now, ct);
    }

    private async Task<decimal> RecordAsync(
        int userId, decimal signedAmount, WalletTransactionType type, string description, string? reference,
        int? createdByUserId, DateTime now, CancellationToken ct)
    {
        // Our UPDATE holds the row lock until commit, so this reads exactly the balance we produced.
        var balanceAfter = await WalletOf(userId).Select(w => w.Balance).SingleAsync(ct);

        db.Add(new WalletTransaction
        {
            UserId = userId,
            Type = type,
            Amount = signedAmount,
            BalanceAfter = balanceAfter,
            Description = description,
            Reference = reference,
            CreatedByUserId = createdByUserId,
            CreatedAtUtc = now,
        });
        await db.SaveChangesAsync(ct); // a repeated Reference fails here, rolling back the balance change too
        return balanceAfter;
    }

    /// <summary>Creates the wallet row on first use. A no-op if it exists; fails loudly for an unknown user.</summary>
    private Task<int> EnsureWalletExistsAsync(int userId, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        return db.Database.ExecuteSqlAsync($"""
            INSERT INTO wallets (UserId, Balance, CreatedAtUtc, UpdatedAtUtc)
            VALUES ({userId}, 0, {now}, {now})
            ON DUPLICATE KEY UPDATE UserId = UserId
            """, ct);
    }

    private static void EnsurePositive(decimal amount)
    {
        if (amount <= 0 || decimal.Round(amount, 2) != amount)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Wallet amounts must be positive with at most 2 decimals.");
        }
    }
}

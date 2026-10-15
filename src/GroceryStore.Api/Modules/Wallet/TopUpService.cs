using GroceryStore.Api.Common;
using GroceryStore.Api.Common.Errors;
using GroceryStore.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MySql.Data.MySqlClient;

namespace GroceryStore.Api.Modules.Wallet;

/// <summary>
/// Adding money: record the attempt, charge through the gateway, then credit the wallet and mark the
/// attempt Succeeded in one transaction. Retries with the same idempotency key return the first result.
/// </summary>
public sealed partial class TopUpService(
    AppDbContext db,
    WalletService wallet,
    IPaymentGateway gateway,
    IOptions<WalletOptions> options,
    TimeProvider timeProvider,
    ILogger<TopUpService> logger)
{
    private const int MySqlDuplicateKeyError = 1062;

    private DbSet<WalletTopUp> TopUps => db.Set<WalletTopUp>();

    public async Task<TopUpResponse> TopUpAsync(int userId, TopUpRequest request, CancellationToken ct)
    {
        if (await FindAsync(userId, request.IdempotencyKey, ct) is { } previous)
        {
            return await ToResponseAsync(previous, ct); // a retry or double tap: never charge again
        }

        // Check the limit BEFORE taking money. (It is re-checked atomically when crediting.)
        var balance = await wallet.GetBalanceAsync(userId, ct);
        var maxBalance = options.Value.MaxBalance;
        if (balance + request.Amount > maxBalance)
        {
            throw new BusinessRuleException(
                $"Your wallet can hold up to {Money.Format(maxBalance)}. You can add at most {Money.Format(Math.Max(0, maxBalance - balance))} right now.");
        }

        var topUp = new WalletTopUp
        {
            UserId = userId,
            Amount = request.Amount,
            Status = TopUpStatus.Pending,
            Provider = gateway.Name,
            IdempotencyKey = request.IdempotencyKey,
            CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime,
        };
        TopUps.Add(topUp);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (e.InnerException is MySqlException { Number: MySqlDuplicateKeyError })
        {
            // A concurrent request with the same key won the race; return its outcome.
            db.Entry(topUp).State = EntityState.Detached;
            return await ToResponseAsync((await FindAsync(userId, request.IdempotencyKey, ct))!, ct);
        }

        var payment = await gateway.ChargeAsync(topUp.Id, topUp.Amount, userId, ct);
        if (!payment.Succeeded)
        {
            await CompleteAsync(topUp, TopUpStatus.Failed, payment.ProviderReference, payment.FailureReason ?? "Payment failed.", ct);
            return await ToResponseAsync(topUp, ct);
        }

        try
        {
            await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var transaction = await db.Database.BeginTransactionAsync(ct);
                await wallet.CreditAsync(userId, topUp.Amount, WalletTransactionType.TopUp, "Money added to wallet",
                    reference: $"topup:{topUp.Id}", createdByUserId: null, maxBalance, ct);
                topUp.Status = TopUpStatus.Succeeded;
                topUp.ProviderReference = payment.ProviderReference;
                topUp.CompletedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            });
        }
        catch (BusinessRuleException limitReached)
        {
            // Charged but can't credit (another top-up landed first). Give the money back; never keep it.
            LogRefundingUncreditedTopUp(logger, topUp.Id, userId);
            await gateway.RefundAsync(payment.ProviderReference!, topUp.Amount, CancellationToken.None);
            await CompleteAsync(topUp, TopUpStatus.Failed, payment.ProviderReference,
                $"Refunded: {limitReached.Message}", CancellationToken.None);
        }

        return await ToResponseAsync(topUp, ct);
    }

    private Task<WalletTopUp?> FindAsync(int userId, string key, CancellationToken ct) =>
        TopUps.AsNoTracking().SingleOrDefaultAsync(t => t.UserId == userId && t.IdempotencyKey == key, ct);

    private async Task CompleteAsync(WalletTopUp topUp, TopUpStatus status, string? providerReference, string? failure, CancellationToken ct)
    {
        topUp.Status = status;
        topUp.ProviderReference = providerReference;
        topUp.FailureReason = failure;
        topUp.CompletedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
    }

    private async Task<TopUpResponse> ToResponseAsync(WalletTopUp t, CancellationToken ct) =>
        new(t.Id, t.Amount, t.Status, t.FailureReason, await wallet.GetBalanceAsync(t.UserId, ct));

    [LoggerMessage(Level = LogLevel.Warning, Message = "Top-up {TopUpId} for user {UserId} was charged but could not be credited; refunding")]
    private static partial void LogRefundingUncreditedTopUp(ILogger logger, long topUpId, int userId);
}

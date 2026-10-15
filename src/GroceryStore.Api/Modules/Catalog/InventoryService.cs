using GroceryStore.Api.Common.Errors;
using GroceryStore.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace GroceryStore.Api.Modules.Catalog;

/// <summary>
/// The only code allowed to change a product's stock. Each change is one atomic, conditional UPDATE
/// (it can never take stock below zero, even under concurrent requests; see the provider spike tests)
/// plus a ledger row in the same transaction.
/// </summary>
public sealed class InventoryService(AppDbContext db, TimeProvider timeProvider)
{
    public Task<int> AdjustAsync(
        int productId, int quantityChange, InventoryReason reason, string? note, int? userId, CancellationToken ct) =>
        db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var stockAfter = await ApplyAsync(productId, quantityChange, reason, note, userId, ct);
            await transaction.CommitAsync(ct);
            return stockAfter;
        });

    /// <summary>
    /// The adjustment itself, for callers that already run inside a transaction
    /// (e.g. product creation now, checkout in Phase 6).
    /// </summary>
    internal async Task<int> ApplyAsync(
        int productId, int quantityChange, InventoryReason reason, string? note, int? userId, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var updated = await db.Set<Product>()
            .Where(p => p.Id == productId && p.StockQuantity + quantityChange >= 0)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.StockQuantity, p => p.StockQuantity + quantityChange)
                .SetProperty(p => p.UpdatedAtUtc, now), ct);

        if (updated == 0)
        {
            var current = await db.Set<Product>().Where(p => p.Id == productId).Select(p => (int?)p.StockQuantity)
                .SingleOrDefaultAsync(ct) ?? throw new NotFoundException("Product not found.");
            throw new BusinessRuleException(
                $"Only {current} unit(s) are in stock, so {-quantityChange} can't be removed.");
        }

        // Our UPDATE holds the row lock until commit, so this read sees exactly the value we produced.
        var stockAfter = await db.Set<Product>().Where(p => p.Id == productId).Select(p => p.StockQuantity).SingleAsync(ct);

        db.Add(new InventoryAdjustment
        {
            ProductId = productId,
            QuantityChange = quantityChange,
            QuantityAfter = stockAfter,
            Reason = reason,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            CreatedByUserId = userId,
            CreatedAtUtc = now,
        });
        await db.SaveChangesAsync(ct);

        return stockAfter;
    }

    public async Task<IReadOnlyList<InventoryAdjustmentResponse>> HistoryAsync(int productId, int take, CancellationToken ct)
    {
        if (!await db.Set<Product>().AnyAsync(p => p.Id == productId, ct))
        {
            throw new NotFoundException("Product not found.");
        }

        return await db.Set<InventoryAdjustment>()
            .Where(a => a.ProductId == productId)
            .OrderByDescending(a => a.CreatedAtUtc).ThenByDescending(a => a.Id)
            .Take(take)
            .Select(a => new InventoryAdjustmentResponse(
                a.Id, a.QuantityChange, a.QuantityAfter, a.Reason, a.Note,
                db.Users.Where(u => u.Id == a.CreatedByUserId).Select(u => u.FullName).FirstOrDefault(),
                a.CreatedAtUtc))
            .ToListAsync(ct);
    }
}

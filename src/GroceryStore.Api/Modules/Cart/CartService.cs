using GroceryStore.Api.Common.Errors;
using GroceryStore.Api.Common.Media;
using GroceryStore.Api.Data;
using GroceryStore.Api.Modules.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GroceryStore.Api.Modules.Cart;

public sealed class CartService(
    AppDbContext db,
    IMediaStorage media,
    IOptions<DeliveryChargesOptions> deliveryCharges,
    TimeProvider timeProvider)
{
    public const int MaxLines = 50;

    private IQueryable<CartItem> ItemsOf(int userId) => db.Set<CartItem>().Where(i => i.UserId == userId);

    // ---------- Reading ----------

    public async Task<CartResponse> GetAsync(int userId, CancellationToken ct)
    {
        var lines = await ItemsOf(userId)
            .OrderBy(i => i.AddedAtUtc).ThenBy(i => i.ProductId)
            .Select(i => new CartLineRequest(i.ProductId, i.Quantity))
            .ToListAsync(ct);
        return await PriceAsync(lines, ct);
    }

    /// <summary>Prices a guest's browser-held cart without storing anything.</summary>
    public Task<CartResponse> PreviewAsync(GuestCartRequest request, CancellationToken ct) =>
        PriceAsync(request.Items.GroupBy(i => i.ProductId).Select(g => new CartLineRequest(g.Key, g.Sum(i => i.Quantity))).ToList(), ct);

    /// <summary>
    /// Turns (product, quantity) pairs into a priced cart using CURRENT catalogue data, flagging any line
    /// that can't be bought as it stands. Shared by the stored cart, guest preview and (Phase 6) checkout.
    /// </summary>
    internal async Task<CartResponse> PriceAsync(IReadOnlyList<CartLineRequest> requested, CancellationToken ct)
    {
        var ids = requested.Select(r => r.ProductId).ToList();
        var products = await db.Set<Product>()
            .Where(p => ids.Contains(p.Id))
            .Select(p => new
            {
                p.Id, p.Name, p.Brand, p.UnitLabel, p.ImagePath, CategoryIcon = p.Category.Icon,
                p.Price, p.Mrp, p.StockQuantity, p.LowStockThreshold, p.MaxPerOrder,
                Visible = p.IsActive && p.Category.IsActive,
            })
            .ToDictionaryAsync(p => p.Id, ct);

        var lines = new List<CartLineResponse>();
        foreach (var line in requested)
        {
            if (!products.TryGetValue(line.ProductId, out var p))
            {
                continue; // deleted outright; nothing meaningful to show
            }

            var maxQuantity = p.Visible ? Math.Max(0, Math.Min(p.MaxPerOrder, p.StockQuantity)) : 0;
            CartLineIssue? issue =
                !p.Visible ? CartLineIssue.Unavailable
                : p.StockQuantity <= 0 ? CartLineIssue.OutOfStock
                : line.Quantity > maxQuantity ? CartLineIssue.ExceedsAvailable
                : null;

            lines.Add(new CartLineResponse(
                p.Id, p.Name, p.Brand, p.UnitLabel,
                p.ImagePath is null ? null : media.PublicUrl(p.ImagePath),
                p.CategoryIcon, p.Price, p.Mrp, line.Quantity, p.Price * line.Quantity, maxQuantity,
                ProductProjections.StatusOf(p.StockQuantity, p.LowStockThreshold), issue));
        }

        var totals = CartPricing.Calculate(
            lines.Where(l => l.Issue is null).Select(l => (l.UnitPrice, l.UnitMrp, l.Quantity)),
            deliveryCharges.Value);
        var hasIssues = lines.Any(l => l.Issue is not null);

        return new CartResponse(lines, totals, hasIssues,
            CanCheckout: lines.Count > 0 && !hasIssues && totals.MeetsMinimumOrder);
    }

    // ---------- Changing ----------

    /// <summary>Sets a product's quantity (0 removes it). Refuses more than can currently be bought.</summary>
    public async Task<CartResponse> SetQuantityAsync(int userId, int productId, int quantity, CancellationToken ct)
    {
        if (quantity == 0)
        {
            return await RemoveAsync(userId, productId, ct);
        }

        var product = await db.Set<Product>()
            .Where(p => p.Id == productId && p.IsActive && p.Category.IsActive)
            .Select(p => new { p.StockQuantity, p.MaxPerOrder })
            .SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException("This product is no longer available.");

        var available = Math.Min(product.MaxPerOrder, product.StockQuantity);
        if (available <= 0)
        {
            throw new BusinessRuleException("Sorry, this product is out of stock.");
        }

        if (quantity > available)
        {
            throw new BusinessRuleException(product.StockQuantity < product.MaxPerOrder
                ? $"Only {available} available right now."
                : $"You can buy at most {available} of this item per order.");
        }

        var isNewLine = !await ItemsOf(userId).AnyAsync(i => i.ProductId == productId, ct);
        if (isNewLine && await ItemsOf(userId).CountAsync(ct) >= MaxLines)
        {
            throw new BusinessRuleException($"Your cart can hold up to {MaxLines} different products.");
        }

        await UpsertAsync(userId, productId, quantity, addToExisting: false, cap: quantity, ct);
        return await GetAsync(userId, ct);
    }

    public async Task<CartResponse> RemoveAsync(int userId, int productId, CancellationToken ct)
    {
        await ItemsOf(userId).Where(i => i.ProductId == productId).ExecuteDeleteAsync(ct);
        return await GetAsync(userId, ct);
    }

    public async Task<CartResponse> ClearAsync(int userId, CancellationToken ct)
    {
        await ItemsOf(userId).ExecuteDeleteAsync(ct);
        return await GetAsync(userId, ct);
    }

    /// <summary>
    /// Adds a guest cart into the customer's saved cart after sign-in. Unlike SetQuantity this never
    /// fails: unavailable products are skipped and quantities are capped at what can be bought, because
    /// the customer didn't ask for this step and shouldn't see errors from it.
    /// </summary>
    public async Task<CartResponse> MergeAsync(int userId, GuestCartRequest request, CancellationToken ct)
    {
        var wanted = request.Items.GroupBy(i => i.ProductId).ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));
        var ids = wanted.Keys.ToList();
        var caps = await db.Set<Product>()
            .Where(p => ids.Contains(p.Id) && p.IsActive && p.Category.IsActive && p.StockQuantity > 0)
            .Select(p => new { p.Id, Cap = p.MaxPerOrder < p.StockQuantity ? p.MaxPerOrder : p.StockQuantity })
            .ToDictionaryAsync(p => p.Id, p => p.Cap, ct);

        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var existing = (await ItemsOf(userId).Select(i => i.ProductId).ToListAsync(ct)).ToHashSet();
            var lineCount = existing.Count;

            foreach (var (productId, quantity) in wanted)
            {
                if (!caps.TryGetValue(productId, out var cap))
                {
                    continue;
                }

                if (!existing.Contains(productId) && ++lineCount > MaxLines)
                {
                    break;
                }

                await UpsertAsync(userId, productId, quantity, addToExisting: true, cap, ct);
            }

            await transaction.CommitAsync(ct);
        });

        return await GetAsync(userId, ct);
    }

    /// <summary>
    /// One atomic INSERT ... ON DUPLICATE KEY UPDATE, so two quick taps on "+" can never create duplicate
    /// rows or fail with a key violation. EF Core has no upsert, hence the (parameterised) SQL.
    /// Uses the MySQL 8.0.19+ row-alias syntax.
    /// </summary>
    private Task<int> UpsertAsync(int userId, int productId, int quantity, bool addToExisting, int cap, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        return addToExisting
            ? db.Database.ExecuteSqlAsync($"""
                INSERT INTO cart_items (UserId, ProductId, Quantity, AddedAtUtc, UpdatedAtUtc)
                VALUES ({userId}, {productId}, LEAST({quantity}, {cap}), {now}, {now}) AS incoming
                ON DUPLICATE KEY UPDATE
                    Quantity = LEAST(cart_items.Quantity + incoming.Quantity, {cap}),
                    UpdatedAtUtc = incoming.UpdatedAtUtc
                """, ct)
            : db.Database.ExecuteSqlAsync($"""
                INSERT INTO cart_items (UserId, ProductId, Quantity, AddedAtUtc, UpdatedAtUtc)
                VALUES ({userId}, {productId}, {quantity}, {now}, {now}) AS incoming
                ON DUPLICATE KEY UPDATE
                    Quantity = incoming.Quantity,
                    UpdatedAtUtc = incoming.UpdatedAtUtc
                """, ct);
    }
}

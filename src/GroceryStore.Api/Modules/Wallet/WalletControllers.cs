using GroceryStore.Api.Common;
using GroceryStore.Api.Common.Errors;
using GroceryStore.Api.Common.Security;
using GroceryStore.Api.Data;
using GroceryStore.Api.Modules.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GroceryStore.Api.Modules.Wallet;

[ApiController]
[Route("api/wallet")]
[Authorize]
[Produces("application/json")]
public sealed class WalletController(WalletService wallet, TopUpService topUps, IOptions<WalletOptions> options) : ControllerBase
{
    [HttpGet]
    public async Task<WalletResponse> Get(CancellationToken ct)
    {
        var limits = options.Value;
        return new WalletResponse(await wallet.GetBalanceAsync(User.GetUserId(), ct), limits.MinTopUp, limits.MaxTopUp, limits.MaxBalance);
    }

    [HttpGet("transactions")]
    public Task<PagedResult<WalletTransactionResponse>> Transactions([FromQuery] PageRequest page, CancellationToken ct) =>
        wallet.GetTransactionsAsync(User.GetUserId(), page, ct);

    /// <summary>
    /// Adds money (simulated payment in this version). Send a new IdempotencyKey per attempt; repeating a
    /// request with the same key returns the original result and never charges twice.
    /// </summary>
    [HttpPost("top-ups")]
    [EnableRateLimiting(RateLimits.Sensitive)]
    [ProducesResponseType<TopUpResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<TopUpResponse> TopUp(TopUpRequest request, CancellationToken ct) =>
        topUps.TopUpAsync(User.GetUserId(), request, ct);
}

[ApiController]
[Route("api/admin/customers/{userId:int}/wallet")]
[Authorize(Policy = Policies.ManageStore)]
[Produces("application/json")]
public sealed class AdminWalletController(AppDbContext db, WalletService wallet) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<AdminWalletResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<AdminWalletResponse> Get(int userId, CancellationToken ct)
    {
        var user = await db.Users.Where(u => u.Id == userId)
            .Select(u => new { u.FullName, u.Email, u.PhoneNumber })
            .SingleOrDefaultAsync(ct) ?? throw new NotFoundException("Customer not found.");
        return new AdminWalletResponse(userId, user.FullName, user.Email!, user.PhoneNumber, await wallet.GetBalanceAsync(userId, ct));
    }

    [HttpGet("transactions")]
    public Task<PagedResult<WalletTransactionResponse>> Transactions(int userId, [FromQuery] PageRequest page, CancellationToken ct) =>
        wallet.GetTransactionsAsync(userId, page, ct);

    /// <summary>Manually credits or debits a wallet. The reason and note are recorded with the admin's name.</summary>
    [HttpPost("adjustments")]
    [EnableRateLimiting(RateLimits.Sensitive)]
    [ProducesResponseType<AdminWalletResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<AdminWalletResponse> Adjust(int userId, AdminWalletAdjustmentRequest request, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(u => u.Id == userId, ct))
        {
            throw new NotFoundException("Customer not found.");
        }

        var description = $"{request.Reason}: {request.Note.Trim()}";
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            if (request.Direction == AdjustmentDirection.Credit)
            {
                await wallet.CreditAsync(userId, request.Amount, WalletTransactionType.AdminCredit, description,
                    reference: null, User.GetUserId(), maxBalance: null, ct);
            }
            else
            {
                await wallet.DebitAsync(userId, request.Amount, WalletTransactionType.AdminDebit, description,
                    reference: null, User.GetUserId(), ct);
            }

            await transaction.CommitAsync(ct);
        });

        return await Get(userId, ct);
    }
}

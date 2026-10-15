using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using FluentValidation;

namespace GroceryStore.Api.Modules.Wallet;

/// <summary>Wallet limits (appsettings "Wallet").</summary>
public sealed class WalletOptions
{
    public const string SectionName = "Wallet";

    [Range(1, 100_000)]
    public decimal MinTopUp { get; set; } = 10m;

    [Range(1, 100_000)]
    public decimal MaxTopUp { get; set; } = 10_000m;

    /// <summary>Most a wallet may hold. Applies to top-ups; refunds and admin credits are never blocked.</summary>
    [Range(1, 1_000_000)]
    public decimal MaxBalance { get; set; } = 20_000m;
}

public sealed record WalletResponse(decimal Balance, decimal MinTopUp, decimal MaxTopUp, decimal MaxBalance);

public sealed record WalletTransactionResponse(
    long Id, WalletTransactionType Type, decimal Amount, decimal BalanceAfter, string Description, DateTime CreatedAtUtc);

/// <param name="IdempotencyKey">A fresh random id per attempt (e.g. a UUID); reuse it only when retrying that attempt.</param>
public sealed record TopUpRequest(decimal Amount, string IdempotencyKey);

internal sealed class TopUpRequestValidator : AbstractValidator<TopUpRequest>
{
    public TopUpRequestValidator(Microsoft.Extensions.Options.IOptions<WalletOptions> options)
    {
        var limits = options.Value;
        RuleFor(r => r.Amount)
            .InclusiveBetween(limits.MinTopUp, limits.MaxTopUp)
            .WithMessage($"Add between {Common.Money.Format(limits.MinTopUp)} and {Common.Money.Format(limits.MaxTopUp)} at a time.")
            .PrecisionScale(10, 2, ignoreTrailingZeros: true).WithMessage("Use at most 2 decimal places (paise).");
        RuleFor(r => r.IdempotencyKey).NotEmpty().MaximumLength(64);
    }
}

public sealed record TopUpResponse(long Id, decimal Amount, TopUpStatus Status, string? FailureReason, decimal Balance);

// ---------- Admin ----------

[JsonConverter(typeof(JsonStringEnumConverter<AdjustmentDirection>))]
public enum AdjustmentDirection
{
    Credit,
    Debit,
}

[JsonConverter(typeof(JsonStringEnumConverter<AdjustmentReason>))]
public enum AdjustmentReason
{
    Refund,
    Goodwill,
    Correction,
}

public sealed record AdminWalletAdjustmentRequest(AdjustmentDirection Direction, decimal Amount, AdjustmentReason Reason, string Note);

internal sealed class AdminWalletAdjustmentRequestValidator : AbstractValidator<AdminWalletAdjustmentRequest>
{
    public AdminWalletAdjustmentRequestValidator()
    {
        RuleFor(r => r.Direction).IsInEnum();
        RuleFor(r => r.Reason).IsInEnum();
        RuleFor(r => r.Amount).GreaterThan(0).WithMessage("Enter an amount above ₹0.")
            .LessThanOrEqualTo(100_000)
            .PrecisionScale(10, 2, ignoreTrailingZeros: true).WithMessage("Use at most 2 decimal places (paise).");
        // Every manual money movement must be explainable later.
        RuleFor(r => r.Note).NotEmpty().WithMessage("Explain why, e.g. the order number or complaint.")
            .MinimumLength(5).WithMessage("Please give a little more detail.")
            .MaximumLength(200);
    }
}

public sealed record AdminWalletResponse(int UserId, string FullName, string Email, string? PhoneNumber, decimal Balance);

using System.Text.Json.Serialization;
using GroceryStore.Api.Modules.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GroceryStore.Api.Modules.Wallet;

/// <summary>A customer's store-credit balance. Created on first credit. Changed only by <see cref="WalletService"/>.</summary>
public class Wallet
{
    public int UserId { get; set; }

    /// <summary>
    /// Cached running total of the ledger, kept in the same transaction as every ledger insert so it is
    /// always equal to SUM(wallet_transactions.Amount). Stored for fast reads and atomic balance checks.
    /// </summary>
    public decimal Balance { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter<WalletTransactionType>))]
public enum WalletTransactionType
{
    TopUp,
    OrderPayment,
    OrderRefund,
    AdminCredit,
    AdminDebit,
}

/// <summary>One append-only ledger entry. Credits are positive amounts, debits negative.</summary>
public class WalletTransaction
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public WalletTransactionType Type { get; set; }
    public decimal Amount { get; set; }
    public decimal BalanceAfter { get; set; }
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// The business event this entry belongs to, e.g. "topup:42" or (Phase 6) "order:17:payment".
    /// Unique, so the database itself refuses to apply the same event twice.
    /// </summary>
    public string? Reference { get; set; }

    /// <summary>The admin who made a manual adjustment; null for customer and system actions.</summary>
    public int? CreatedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter<TopUpStatus>))]
public enum TopUpStatus
{
    Pending,
    Succeeded,
    Failed,
}

/// <summary>
/// A request to add money, tracked separately from the ledger because with a real gateway it is not
/// instant: it is created Pending, the customer pays on the gateway's page, and the gateway's
/// confirmation (webhook) marks it Succeeded or Failed. Only a Succeeded top-up credits the wallet.
/// </summary>
public class WalletTopUp
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public decimal Amount { get; set; }
    public TopUpStatus Status { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string? ProviderReference { get; set; }
    public string? FailureReason { get; set; }

    /// <summary>Client-generated per attempt; repeating a request with the same key never charges twice.</summary>
    public string IdempotencyKey { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}

internal sealed class WalletConfiguration : IEntityTypeConfiguration<Wallet>
{
    public void Configure(EntityTypeBuilder<Wallet> builder)
    {
        builder.ToTable("wallets", t => t.HasCheckConstraint("CK_wallets_balance_not_negative", "`Balance` >= 0"));
        builder.HasKey(w => w.UserId);
        builder.HasOne<AppUser>().WithOne().HasForeignKey<Wallet>(w => w.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class WalletTransactionConfiguration : IEntityTypeConfiguration<WalletTransaction>
{
    public void Configure(EntityTypeBuilder<WalletTransaction> builder)
    {
        builder.ToTable("wallet_transactions");
        builder.HasOne<Wallet>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(t => t.CreatedByUserId).OnDelete(DeleteBehavior.SetNull);
        builder.Property(t => t.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.Description).HasMaxLength(250);
        builder.Property(t => t.Reference).HasMaxLength(64);
        builder.HasIndex(t => t.Reference).IsUnique();
        builder.HasIndex(t => new { t.UserId, t.CreatedAtUtc });
    }
}

internal sealed class WalletTopUpConfiguration : IEntityTypeConfiguration<WalletTopUp>
{
    public void Configure(EntityTypeBuilder<WalletTopUp> builder)
    {
        builder.ToTable("wallet_top_ups", t => t.HasCheckConstraint("CK_wallet_top_ups_amount_positive", "`Amount` > 0"));
        builder.HasOne<AppUser>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(12);
        builder.Property(t => t.Provider).HasMaxLength(30);
        builder.Property(t => t.ProviderReference).HasMaxLength(100);
        builder.Property(t => t.FailureReason).HasMaxLength(200);
        builder.Property(t => t.IdempotencyKey).HasMaxLength(64);
        builder.HasIndex(t => new { t.UserId, t.IdempotencyKey }).IsUnique();
    }
}

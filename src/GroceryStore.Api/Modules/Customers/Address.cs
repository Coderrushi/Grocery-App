using System.Text.Json.Serialization;
using GroceryStore.Api.Modules.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GroceryStore.Api.Modules.Customers;

[JsonConverter(typeof(JsonStringEnumConverter<AddressLabel>))]
public enum AddressLabel
{
    Home,
    Work,
    Other,
}

/// <summary>
/// A saved delivery address. Orders (Phase 6) will copy the address text at checkout, so editing or
/// deleting an address here never rewrites the history of a past order. That is why hard delete is safe.
/// </summary>
public class Address
{
    public const int MaxPerCustomer = 10;

    public int Id { get; set; }
    public int UserId { get; set; }
    public AddressLabel Label { get; set; }
    public string RecipientName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>House / flat number, building.</summary>
    public string Line1 { get; set; } = string.Empty;

    /// <summary>Street, area, locality.</summary>
    public string? Line2 { get; set; }

    public string? Landmark { get; set; }
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Pincode { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

internal sealed class AddressConfiguration : IEntityTypeConfiguration<Address>
{
    public void Configure(EntityTypeBuilder<Address> builder)
    {
        builder.ToTable("addresses");
        builder.HasOne<AppUser>().WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(a => a.UserId);

        builder.Property(a => a.Label).HasConversion<string>().HasMaxLength(10);
        builder.Property(a => a.RecipientName).HasMaxLength(100);
        builder.Property(a => a.PhoneNumber).HasMaxLength(10);
        builder.Property(a => a.Line1).HasMaxLength(150);
        builder.Property(a => a.Line2).HasMaxLength(150);
        builder.Property(a => a.Landmark).HasMaxLength(100);
        builder.Property(a => a.City).HasMaxLength(60);
        builder.Property(a => a.State).HasMaxLength(60);
        builder.Property(a => a.Pincode).HasMaxLength(6).IsFixedLength();
    }
}

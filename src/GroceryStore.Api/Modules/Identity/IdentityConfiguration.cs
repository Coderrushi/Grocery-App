using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GroceryStore.Api.Modules.Identity;

internal sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.Property(u => u.FullName).HasMaxLength(100);

        // Customers can sign in with their mobile number, so it must be unique. Stored as 10 digits, no +91.
        // MySQL unique indexes allow multiple NULLs, so accounts without a phone are unaffected.
        builder.Property(u => u.PhoneNumber).HasMaxLength(10);
        builder.HasIndex(u => u.PhoneNumber).IsUnique();
    }
}

internal sealed class AppRoleConfiguration : IEntityTypeConfiguration<AppRole>
{
    public void Configure(EntityTypeBuilder<AppRole> builder)
    {
        // Fixed ids and concurrency stamps keep the seed deterministic, so migrations don't churn.
        builder.HasData(
            new AppRole
            {
                Id = 1,
                Name = Roles.Admin,
                NormalizedName = Roles.Admin.ToUpperInvariant(),
                ConcurrencyStamp = "8d3c6f0e-5f0e-4d8a-9a3b-2a1f4c1e0a01",
            },
            new AppRole
            {
                Id = 2,
                Name = Roles.Customer,
                NormalizedName = Roles.Customer.ToUpperInvariant(),
                ConcurrencyStamp = "8d3c6f0e-5f0e-4d8a-9a3b-2a1f4c1e0a02",
            });
    }
}

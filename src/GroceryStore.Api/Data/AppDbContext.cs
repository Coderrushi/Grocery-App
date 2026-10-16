using GroceryStore.Api.Data.Conventions;
using GroceryStore.Api.Modules.Identity;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GroceryStore.Api.Data;

/// <summary>
/// The single DbContext for the modular monolith. Each module contributes its entities through
/// IEntityTypeConfiguration classes in its own folder; they are discovered automatically below.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<AppUser, AppRole, int>(options), IDataProtectionKeyContext
{
    /// <summary>
    /// The keys that encrypt auth cookies (table data_protection_keys). Kept in the database so a restart
    /// or redeploy doesn't sign everyone out, and every instance can read every cookie (ADR 0004).
    /// </summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        MySqlModelConventions.Apply(configurationBuilder);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        MySqlModelConventions.Apply(builder);
    }
}

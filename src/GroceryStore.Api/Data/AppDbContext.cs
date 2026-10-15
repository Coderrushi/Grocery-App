using GroceryStore.Api.Data.Conventions;
using GroceryStore.Api.Modules.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GroceryStore.Api.Data;

/// <summary>
/// The single DbContext for the modular monolith. Each module contributes its entities through
/// IEntityTypeConfiguration classes in its own folder; they are discovered automatically below.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<AppUser, AppRole, int>(options)
{
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

using GroceryStore.Api.Data.Conventions;
using Microsoft.EntityFrameworkCore;

namespace GroceryStore.Tests.Integration.ProviderSpike;

// A deliberately tiny model, separate from the real AppDbContext, used to pin down how the
// Oracle MySQL EF Core provider behaves for the patterns the application relies on.

public sealed class SpikeProduct
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public int Version { get; set; }
}

public sealed class SpikeOrder
{
    public int Id { get; set; }
    public DateTime PlacedAtUtc { get; set; }
    public decimal Total { get; set; }
}

public sealed class SpikeDbContext(DbContextOptions<SpikeDbContext> options) : DbContext(options)
{
    public DbSet<SpikeProduct> SpikeProducts => Set<SpikeProduct>();
    public DbSet<SpikeOrder> SpikeOrders => Set<SpikeOrder>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
        MySqlModelConventions.Apply(configurationBuilder);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SpikeProduct>(b =>
        {
            b.Property(p => p.Name).HasMaxLength(200);
            b.Property(p => p.Version).IsConcurrencyToken();
        });
        MySqlModelConventions.Apply(modelBuilder);
    }
}

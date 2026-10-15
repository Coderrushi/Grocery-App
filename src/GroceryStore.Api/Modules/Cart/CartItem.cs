using GroceryStore.Api.Modules.Catalog;
using GroceryStore.Api.Modules.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GroceryStore.Api.Modules.Cart;

/// <summary>
/// One product in a signed-in customer's cart. A customer's cart is simply their set of rows; there is
/// no separate "cart" record until a requirement (e.g. one cart per store) needs one.
/// Prices are deliberately NOT stored: the cart always shows current prices, and checkout re-prices.
/// </summary>
public class CartItem
{
    public int UserId { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public DateTime AddedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

internal sealed class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    public const string TableName = "cart_items";

    public void Configure(EntityTypeBuilder<CartItem> builder)
    {
        builder.ToTable(TableName, t => t.HasCheckConstraint("CK_cart_items_quantity_positive", "`Quantity` > 0"));
        // The composite key is also what makes the upsert in CartService atomic.
        builder.HasKey(i => new { i.UserId, i.ProductId });
        builder.HasOne<AppUser>().WithMany().HasForeignKey(i => i.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Product>().WithMany().HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Cascade);
    }
}

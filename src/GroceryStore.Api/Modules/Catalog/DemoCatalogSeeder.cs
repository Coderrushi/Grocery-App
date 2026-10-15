using GroceryStore.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace GroceryStore.Api.Modules.Catalog;

/// <summary>
/// Fills an EMPTY catalogue with sample groceries so the storefront can be demonstrated and tested by
/// hand. Runs only when "SeedDemoCatalog" is true (appsettings.Development.json), never in production,
/// so demo items can never mix with the shop's real products.
/// </summary>
internal static class DemoCatalogSeeder
{
    private sealed record DemoProduct(
        string Name, string? Brand, string Unit, decimal Price, decimal Mrp, int Stock,
        bool Featured = false, string? Description = null);

    private static readonly (string Name, string Icon, DemoProduct[] Products)[] Data =
    [
        ("Fruits & Vegetables", "🥬",
        [
            new("Onion", null, "1 kg", 38, 45, 120, Featured: true, "Fresh red onions, sorted and cleaned."),
            new("Potato", null, "1 kg", 32, 36, 150),
            new("Tomato (Hybrid)", null, "1 kg", 42, 50, 80, Featured: true),
            new("Banana (Robusta)", null, "1 dozen", 55, 60, 40),
            new("Shimla Apple", null, "1 kg", 180, 220, 25, Description: "Crisp, sweet apples from Himachal Pradesh."),
            new("Coriander Leaves", null, "100 g", 15, 20, 4),
            new("Green Chillies", null, "100 g", 12, 15, 30),
        ]),
        ("Dairy, Bread & Eggs", "🥛",
        [
            new("Taaza Toned Milk", "Amul", "500 ml", 28, 28, 60, Featured: true),
            new("Fresh Paneer", "Amul", "200 g", 90, 95, 18),
            new("Salted Butter", "Amul", "100 g", 58, 60, 35),
            new("Brown Bread", "Britannia", "400 g", 50, 55, 3),
            new("Farm Eggs", null, "Pack of 6", 48, 54, 22),
            new("Masti Dahi", "Amul", "400 g", 35, 35, 0),
        ]),
        ("Atta, Rice & Dals", "🌾",
        [
            new("Whole Wheat Atta", "Aashirvaad", "5 kg", 255, 295, 30, Featured: true),
            new("Basmati Rice", "India Gate", "1 kg", 145, 175, 25),
            new("Sona Masoori Rice", null, "5 kg", 340, 375, 15),
            new("Toor Dal", "Tata Sampann", "1 kg", 168, 199, 20),
            new("Moong Dal", null, "500 g", 72, 80, 18),
            new("Kabuli Chana", null, "500 g", 85, 95, 12),
        ]),
        ("Oils, Ghee & Masalas", "🫙",
        [
            new("Refined Sunflower Oil", "Fortune", "1 L", 155, 180, 40),
            new("Pure Cow Ghee", "Amul", "500 ml", 310, 330, 15),
            new("Turmeric Powder", "Everest", "100 g", 32, 36, 30),
            new("Garam Masala", "MDH", "100 g", 82, 90, 25),
            new("Iodised Salt", "Tata", "1 kg", 28, 28, 60),
        ]),
        ("Snacks & Biscuits", "🍪",
        [
            new("Good Day Cashew Cookies", "Britannia", "200 g", 40, 45, 50, Featured: true),
            new("Aloo Bhujia", "Haldiram's", "400 g", 105, 115, 20),
            new("Marie Gold", "Britannia", "250 g", 30, 30, 45),
            new("Classic Salted Chips", "Lay's", "52 g", 20, 20, 2),
        ]),
        ("Beverages", "🧃",
        [
            new("Premium Tea", "Tata Tea", "500 g", 255, 280, 20),
            new("Classic Instant Coffee", "Nescafé", "50 g", 165, 175, 15),
            new("Mango Drink", "Maaza", "1.2 L", 70, 75, 30),
            new("Packaged Drinking Water", "Bisleri", "1 L", 20, 20, 100),
        ]),
        ("Cleaning & Household", "🧼",
        [
            new("Dishwash Bar", "Vim", "Pack of 3", 30, 33, 40),
            new("Detergent Powder", "Surf Excel", "1 kg", 135, 145, 25),
            new("Floor Cleaner", "Lizol", "500 ml", 105, 119, 18),
        ]),
        ("Personal Care", "🧴",
        [
            new("Bathing Soap", "Lux", "Pack of 4", 140, 160, 30),
            new("Toothpaste", "Colgate", "200 g", 110, 120, 25),
            new("Coconut Hair Oil", "Parachute", "300 ml", 135, 145, 20),
        ]),
    ];

    public static async Task SeedDemoCatalogAsync(this WebApplication app)
    {
        if (!app.Configuration.GetValue<bool>("SeedDemoCatalog"))
        {
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (await db.Set<Category>().AnyAsync())
        {
            return; // never touch a catalogue that already has data
        }

        var now = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var created = new List<Product>();
        var sortOrder = 0;
        foreach (var (name, icon, products) in Data)
        {
            var category = new Category
            {
                Name = name,
                Slug = Common.Slugs.From(name),
                Icon = icon,
                SortOrder = sortOrder += 10,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            };
            db.Add(category);

            created.AddRange(products.Select(p => new Product
            {
                Category = category,
                Name = p.Name,
                Brand = p.Brand,
                UnitLabel = p.Unit,
                Price = p.Price,
                Mrp = p.Mrp,
                StockQuantity = p.Stock,
                IsFeatured = p.Featured,
                Description = p.Description,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            }));
        }

        db.AddRange(created);
        await db.SaveChangesAsync(); // assigns product ids

        // Opening stock goes through the ledger like any other stock, so the history is complete.
        db.AddRange(created.Where(p => p.StockQuantity > 0).Select(p => new InventoryAdjustment
        {
            ProductId = p.Id,
            QuantityChange = p.StockQuantity,
            QuantityAfter = p.StockQuantity,
            Reason = InventoryReason.InitialStock,
            Note = "Demo data",
            CreatedAtUtc = now,
        }));
        await db.SaveChangesAsync();
    }
}

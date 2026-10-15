namespace GroceryStore.Api.Modules.Catalog;

internal static class CatalogSetup
{
    public static IServiceCollection AddCatalog(this IServiceCollection services) => services
        .AddScoped<CatalogQueryService>()
        .AddScoped<AdminCategoryService>()
        .AddScoped<AdminProductService>()
        .AddScoped<InventoryService>();
}

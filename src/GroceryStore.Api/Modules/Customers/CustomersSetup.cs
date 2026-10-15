namespace GroceryStore.Api.Modules.Customers;

internal static class CustomersSetup
{
    public static IServiceCollection AddCustomers(this IServiceCollection services) =>
        services.AddScoped<AddressService>();
}

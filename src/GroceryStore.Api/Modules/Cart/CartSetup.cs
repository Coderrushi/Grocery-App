namespace GroceryStore.Api.Modules.Cart;

internal static class CartSetup
{
    public static IServiceCollection AddCart(this IServiceCollection services)
    {
        services.AddOptions<DeliveryChargesOptions>()
            .BindConfiguration(DeliveryChargesOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart(); // a typo in the delivery fee should stop the app, not reach customers
        return services.AddScoped<CartService>();
    }
}

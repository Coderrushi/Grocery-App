namespace GroceryStore.Api.Modules.Orders;

internal static class OrdersSetup
{
    public static IServiceCollection AddOrders(this IServiceCollection services)
    {
        services.AddOptions<DeliveryAreaOptions>()
            .BindConfiguration(DeliveryAreaOptions.SectionName)
            .Validate(o => o.ServiceablePincodes.All(p => p.Length == 6 && p.All(char.IsAsciiDigit)),
                "DeliveryArea:ServiceablePincodes must contain 6-digit PIN codes.")
            .ValidateOnStart();

        return services
            .AddScoped<OrderService>()
            .AddScoped<AdminOrderService>();
    }
}

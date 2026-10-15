namespace GroceryStore.Api.Modules.Wallet;

internal static class WalletSetup
{
    public static IServiceCollection AddWallet(this IServiceCollection services)
    {
        services.AddOptions<WalletOptions>()
            .BindConfiguration(WalletOptions.SectionName)
            .ValidateDataAnnotations()
            .Validate(o => o.MinTopUp <= o.MaxTopUp && o.MaxTopUp <= o.MaxBalance,
                "Wallet limits must satisfy MinTopUp <= MaxTopUp <= MaxBalance.")
            .ValidateOnStart();

        // Swap this one registration for a real provider's gateway when going live (ADR 0003).
        services.AddSingleton<IPaymentGateway, SimulatedPaymentGateway>();

        return services
            .AddScoped<WalletService>()
            .AddScoped<TopUpService>();
    }
}

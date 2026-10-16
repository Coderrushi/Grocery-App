namespace GroceryStore.Api.Modules.Reports;

internal static class ReportsSetup
{
    public static IServiceCollection AddReports(this IServiceCollection services) =>
        services.AddScoped<ReportService>();
}

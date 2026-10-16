using System.Diagnostics;
using System.Text.Json.Serialization;
using GroceryStore.Api.Common;
using GroceryStore.Api.Common.Errors;
using GroceryStore.Api.Common.Hosting;
using GroceryStore.Api.Common.Media;
using GroceryStore.Api.Common.Security;
using GroceryStore.Api.Common.Validation;
using GroceryStore.Api.Data;
using GroceryStore.Api.Modules.Cart;
using GroceryStore.Api.Modules.Catalog;
using GroceryStore.Api.Modules.Customers;
using GroceryStore.Api.Modules.Identity;
using GroceryStore.Api.Modules.Orders;
using GroceryStore.Api.Modules.Reports;
using GroceryStore.Api.Modules.Wallet;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// ---------- Services ----------

builder.Services.AddDatabase(builder.Configuration);
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = context =>
    {
        // Lets support staff match an error the user reports with the server log entry.
        context.ProblemDetails.Extensions["traceId"] =
            Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
        context.ProblemDetails.Instance ??= context.HttpContext.Request.Path;
    });
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddAppValidation();

builder.Services
    .AddControllers(options =>
    {
        options.Filters.Add<FluentValidationFilter>();
        // FluentValidation is the single source of validation rules and messages.
        options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
    })
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddOpenApi();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database", tags: [ReadyTag]);

builder.AddHostingSecurity(); // proxies, cookie-key storage, request size
builder.Services.AddAppRateLimiting();

// Modules
builder.Services.AddAppIdentity(builder.Configuration, builder.Environment);
builder.Services.AddCustomers();
builder.Services.AddCatalog();
builder.Services.AddCart();
builder.Services.AddWallet();
builder.Services.AddOrders();
builder.Services.AddReports();
builder.Services.AddMedia(builder.Configuration, builder.Environment);

// ---------- HTTP pipeline ----------

var app = builder.Build();
var spa = SpaHosting.Find(app); // the built React app in production; null in development (Vite serves it)

app.UseForwardedHeaders(); // first: everything after sees the real client IP and https scheme
app.UseSecurityHeaders(spa?.ContentSecurityPolicy);
app.UseExceptionHandler();
app.UseStatusCodePages(); // Gives bare 401/403/404/429 responses a ProblemDetails body too.

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous(); // /openapi/v1.json
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "QuickKirana API v1"));
}
else
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseMedia(); // uploaded product images under /media (public, before auth)
if (spa is not null)
{
    app.UseSpa(spa); // the web app's files, and index.html for every other non-API path
}

app.UseAuthentication();
app.UseRateLimiter(); // after authentication (per-user limits), before authorization (floods are counted too)
// Nothing matched (unknown route, missing media file, unknown /api URL): answer 404 here. Otherwise the
// deny-by-default fallback policy would turn every unknown URL into 401 for visitors.
app.Use((context, next) =>
{
    if (SpaHosting.IsUnmatched(context))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return Task.CompletedTask;
    }

    return next(context);
});
app.UseAuthorization();

app.MapControllers();

// Liveness: the process answers (no dependencies; a failing database must not get the app restarted).
// Readiness: the database is reachable, so traffic can be sent. /api/health is kept as an alias of ready.
var live = new HealthCheckOptions { Predicate = _ => false, ResponseWriter = HealthResponseWriter.WriteAsync };
var ready = new HealthCheckOptions { Predicate = check => check.Tags.Contains(ReadyTag), ResponseWriter = HealthResponseWriter.WriteAsync };
app.MapHealthChecks("/api/health/live", live).AllowAnonymous();
app.MapHealthChecks("/api/health/ready", ready).AllowAnonymous();
app.MapHealthChecks("/api/health", ready).AllowAnonymous();

await app.MigrateDatabaseIfEnabledAsync();
await app.SeedAdminAsync();
await app.SeedDemoCatalogAsync();
await app.RunAsync();

// Exposes the implicit Program class to WebApplicationFactory in the integration tests.
public partial class Program
{
    private const string ReadyTag = "ready";
}

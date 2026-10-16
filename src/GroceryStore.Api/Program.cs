using System.Diagnostics;
using System.Text.Json.Serialization;
using GroceryStore.Api.Common;
using GroceryStore.Api.Common.Errors;
using GroceryStore.Api.Common.Media;
using GroceryStore.Api.Common.Validation;
using GroceryStore.Api.Data;
using GroceryStore.Api.Modules.Cart;
using GroceryStore.Api.Modules.Catalog;
using GroceryStore.Api.Modules.Customers;
using GroceryStore.Api.Modules.Identity;
using GroceryStore.Api.Modules.Orders;
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
    .AddDbContextCheck<AppDbContext>("database");

// Modules
builder.Services.AddAppIdentity(builder.Configuration, builder.Environment);
builder.Services.AddCustomers();
builder.Services.AddCatalog();
builder.Services.AddCart();
builder.Services.AddWallet();
builder.Services.AddOrders();
builder.Services.AddMedia(builder.Configuration, builder.Environment);

// ---------- HTTP pipeline ----------

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages(); // Gives bare 401/403/404/429 responses a ProblemDetails body too.

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi(); // /openapi/v1.json
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "QuickKirana API v1"));
}
else
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseMedia(); // uploaded product images under /media (public, before auth)

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();
app.MapHealthChecks("/api/health", new HealthCheckOptions { ResponseWriter = HealthResponseWriter.WriteAsync });

await app.SeedAdminAsync();
await app.SeedDemoCatalogAsync();
await app.RunAsync();

// Exposes the implicit Program class to WebApplicationFactory in the integration tests.
public partial class Program;

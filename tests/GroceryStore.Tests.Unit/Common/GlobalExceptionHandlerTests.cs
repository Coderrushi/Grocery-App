using System.Text.Json;
using GroceryStore.Api.Common.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace GroceryStore.Tests.Unit.Common;

public sealed class GlobalExceptionHandlerTests
{
    private static async Task<(int Status, JsonElement Body)> HandleAsync(Exception exception)
    {
        var services = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Response.Body = new MemoryStream();
        context.Request.Headers.Accept = "application/json";

        var handler = new GlobalExceptionHandler(
            services.GetRequiredService<IProblemDetailsService>(),
            NullLogger<GlobalExceptionHandler>.Instance);

        var handled = await handler.TryHandleAsync(context, exception, TestContext.Current.CancellationToken);

        Assert.True(handled);
        context.Response.Body.Position = 0;
        var body = await JsonSerializer.DeserializeAsync<JsonElement>(context.Response.Body, cancellationToken: TestContext.Current.CancellationToken);
        return (context.Response.StatusCode, body);
    }

    [Theory]
    [InlineData(typeof(NotFoundException), 404)]
    [InlineData(typeof(ConflictException), 409)]
    [InlineData(typeof(BusinessRuleException), 422)]
    public async Task Business_exceptions_map_to_their_status_and_keep_the_message(Type exceptionType, int expectedStatus)
    {
        var exception = (Exception)Activator.CreateInstance(exceptionType, "Only 2 left in stock.")!;

        var (status, body) = await HandleAsync(exception);

        Assert.Equal(expectedStatus, status);
        Assert.Equal(expectedStatus, body.GetProperty("status").GetInt32());
        Assert.Equal("Only 2 left in stock.", body.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Unexpected_exceptions_return_500_without_leaking_details()
    {
        var (status, body) = await HandleAsync(new InvalidOperationException("Connection string has Password=secret"));

        Assert.Equal(500, status);
        Assert.DoesNotContain("secret", body.GetRawText(), StringComparison.Ordinal);
    }
}

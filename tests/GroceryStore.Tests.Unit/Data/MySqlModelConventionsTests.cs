using GroceryStore.Api.Data.Conventions;

namespace GroceryStore.Tests.Unit.Data;

public sealed class MySqlModelConventionsTests
{
    [Theory]
    [InlineData("Products", "products")]
    [InlineData("OrderItems", "order_items")]
    [InlineData("UserRoles", "user_roles")]
    [InlineData("WalletTransactions", "wallet_transactions")]
    [InlineData("SKUCodes", "sku_codes")]
    [InlineData("already_snake", "already_snake")]
    public void ToSnakeCase_produces_lower_case_table_names(string input, string expected) =>
        Assert.Equal(expected, MySqlModelConventions.ToSnakeCase(input));

    [Fact]
    public void Utc_converter_marks_values_read_from_the_database_as_utc()
    {
        var converter = new UtcDateTimeConverter();
        var fromDb = new DateTime(2026, 10, 15, 8, 0, 0, DateTimeKind.Unspecified);

        var result = (DateTime)converter.ConvertFromProvider(fromDb)!;

        Assert.Equal(DateTimeKind.Utc, result.Kind);
        Assert.Equal(fromDb.Ticks, result.Ticks);
    }

    [Fact]
    public void Utc_converter_converts_local_times_to_utc_before_saving()
    {
        var converter = new UtcDateTimeConverter();
        var local = new DateTime(2026, 10, 15, 13, 30, 0, DateTimeKind.Local);

        var stored = (DateTime)converter.ConvertToProvider(local)!;

        Assert.Equal(local.ToUniversalTime(), stored);
    }
}

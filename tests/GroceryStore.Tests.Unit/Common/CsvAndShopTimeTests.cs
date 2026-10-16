using System.Globalization;
using GroceryStore.Api.Common;
using GroceryStore.Api.Modules.Orders;
using GroceryStore.Api.Modules.Reports;

namespace GroceryStore.Tests.Unit.Common;

public sealed class CsvBuilderTests
{
    private static string Line(params object?[] cells) => new CsvBuilder().Row(cells).ToString().TrimEnd('\r', '\n');

    [Fact]
    public void Numbers_use_invariant_format_and_money_has_two_decimals() =>
        Assert.Equal("1234.50,-3,12", Line(1234.5m, -3, 12L));

    [Fact]
    public void Text_with_commas_quotes_or_line_breaks_is_quoted() =>
        Assert.Equal("\"Rice, Basmati\",\"5\"\" pan\",\"line\nbreak\",plain", Line("Rice, Basmati", "5\" pan", "line\nbreak", "plain"));

    [Theory]
    [InlineData("=SUM(A1:A9)", "'=SUM(A1:A9)")]
    [InlineData("+91 98765", "'+91 98765")]
    [InlineData("-cmd", "'-cmd")]
    [InlineData("@me", "'@me")]
    [InlineData("Asha = good", "Asha = good")]
    public void Text_that_a_spreadsheet_would_run_as_a_formula_is_defused(string text, string expected) =>
        Assert.Equal(expected, Line(text));

    [Fact]
    public void Dates_are_iso_and_utc_timestamps_are_shown_in_ist() =>
        Assert.Equal(
            "2026-10-05,2026-10-06 01:00,,Delivered",
            Line(new DateOnly(2026, 10, 5), new DateTime(2026, 10, 5, 19, 30, 0, DateTimeKind.Utc), null, OrderStatus.Delivered));

    [Fact]
    public void Download_starts_with_a_utf8_byte_order_mark_for_excel()
    {
        var file = new CsvBuilder("Name").Row("₹ धनिया").ToFile("x.csv");

        Assert.Equal([0xEF, 0xBB, 0xBF], file.FileContents[..3]);
        Assert.Equal("Name\r\n₹ धनिया\r\n", System.Text.Encoding.UTF8.GetString(file.FileContents[3..]));
        Assert.Equal("x.csv", file.FileDownloadName);
    }
}

public sealed class ShopTimeTests
{
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    [Theory]
    [InlineData("2026-10-15T18:29:00Z", "2026-10-15")] // 23:59 IST
    [InlineData("2026-10-15T18:30:00Z", "2026-10-16")] // midnight IST: already the next day in India
    public void Today_is_the_indian_date(string utcNow, string expected) =>
        Assert.Equal(
            DateOnly.Parse(expected, CultureInfo.InvariantCulture),
            ShopTime.Today(new FixedClock(DateTimeOffset.Parse(utcNow, CultureInfo.InvariantCulture))));

    [Fact]
    public void An_indian_day_starts_at_1830_utc_the_day_before()
    {
        var start = ShopTime.StartOfDayUtc(new DateOnly(2026, 10, 16));

        Assert.Equal(new DateTime(2026, 10, 15, 18, 30, 0), start);
        Assert.Equal(DateTimeKind.Utc, start.Kind);
    }

    [Fact]
    public void A_range_covers_whole_indian_days_both_ends_included()
    {
        var range = new ResolvedRange(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 3));

        Assert.Equal(new DateTime(2026, 9, 30, 18, 30, 0), range.FromUtc);
        Assert.Equal(new DateTime(2026, 10, 3, 18, 30, 0), range.ToUtcExclusive);
        Assert.Equal(3, range.Days.Count());
    }
}

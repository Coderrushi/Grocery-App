using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace GroceryStore.Api.Common;

/// <summary>
/// Builds an RFC 4180 CSV download that opens correctly in Excel and Google Sheets:
/// UTF-8 with a byte-order mark (so "₹" and Hindi names survive), invariant numbers (1234.50, no
/// grouping), dates as yyyy-MM-dd and times in IST.
/// Text cells that start with = + - @ are prefixed with an apostrophe, so a customer named
/// "=HYPERLINK(...)" cannot become a live formula in the shopkeeper's spreadsheet (CSV injection).
/// </summary>
public sealed class CsvBuilder
{
    private static readonly UTF8Encoding Utf8WithBom = new(encoderShouldEmitUTF8Identifier: true);
    private readonly StringBuilder _text = new();

    public CsvBuilder(params string[] header)
    {
        if (header.Length > 0)
        {
            Row([.. header]);
        }
    }

    public CsvBuilder Row(params object?[] cells)
    {
        _text.AppendJoin(',', cells.Select(Format)).Append("\r\n");
        return this;
    }

    public FileContentResult ToFile(string fileName) =>
        new([.. Utf8WithBom.GetPreamble(), .. Utf8WithBom.GetBytes(_text.ToString())], "text/csv; charset=utf-8")
        {
            FileDownloadName = fileName,
        };

    public override string ToString() => _text.ToString();

    private static string Format(object? value) => value switch
    {
        null => string.Empty,
        string text => Quote(Neutralise(text)),
        decimal amount => amount.ToString("0.00", CultureInfo.InvariantCulture),
        DateOnly day => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateTime utc => ShopTime.ToShopTime(utc).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
        IFormattable formattable => Quote(formattable.ToString(null, CultureInfo.InvariantCulture)),
        _ => Quote(value.ToString() ?? string.Empty),
    };

    private static string Neutralise(string text) =>
        text.Length > 0 && text[0] is '=' or '+' or '-' or '@' or '\t' or '\r' ? "'" + text : text;

    private static string Quote(string text) =>
        text.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? $"\"{text.Replace("\"", "\"\"", StringComparison.Ordinal)}\"" : text;
}

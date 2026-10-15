using System.Globalization;

namespace GroceryStore.Api.Common;

public static class Money
{
    private static readonly CultureInfo India = CultureInfo.GetCultureInfo("en-IN");

    /// <summary>1234.5 -> "₹1,234.50" (for messages shown to users).</summary>
    public static string Format(decimal amount) => amount.ToString("C", India);
}

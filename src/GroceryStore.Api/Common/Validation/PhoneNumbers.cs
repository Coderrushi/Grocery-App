namespace GroceryStore.Api.Common.Validation;

public static class PhoneNumbers
{
    /// <summary>
    /// Reduces common ways of typing an Indian mobile number ("+91 98765 43210", "098765-43210")
    /// to the 10-digit form stored in the database. Used where users type free text, e.g. the login box.
    /// </summary>
    public static string Normalize(string input)
    {
        var digits = new string([.. input.Where(char.IsAsciiDigit)]);
        if (digits.Length == 12 && digits.StartsWith("91", StringComparison.Ordinal))
        {
            return digits[2..];
        }

        if (digits.Length == 11 && digits[0] == '0')
        {
            return digits[1..];
        }

        return digits;
    }
}

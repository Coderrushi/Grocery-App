using System.Globalization;
using System.Text;

namespace GroceryStore.Api.Common;

public static class Slugs
{
    /// <summary>"Fruits & Vegetables" -> "fruits-vegetables". Accents are removed; other scripts are dropped.</summary>
    public static string From(string text)
    {
        var builder = new StringBuilder(text.Length);
        var pendingHyphen = false;
        foreach (var c in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue; // é -> e
            }

            if (char.IsAsciiLetterOrDigit(c))
            {
                if (pendingHyphen && builder.Length > 0)
                {
                    builder.Append('-');
                }

                builder.Append(char.ToLowerInvariant(c));
                pendingHyphen = false;
            }
            else
            {
                pendingHyphen = true;
            }
        }

        return builder.ToString();
    }
}

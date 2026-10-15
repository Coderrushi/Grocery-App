using System.Text.RegularExpressions;
using FluentValidation;

namespace GroceryStore.Api.Common.Validation;

/// <summary>Reusable validation rules for Indian phone numbers, PIN codes and states.</summary>
public static partial class IndiaRules
{
    /// <summary>States and union territories, as shown in the address form's dropdown.</summary>
    public static readonly IReadOnlyList<string> States =
    [
        "Andaman and Nicobar Islands", "Andhra Pradesh", "Arunachal Pradesh", "Assam", "Bihar", "Chandigarh",
        "Chhattisgarh", "Dadra and Nagar Haveli and Daman and Diu", "Delhi", "Goa", "Gujarat", "Haryana",
        "Himachal Pradesh", "Jammu and Kashmir", "Jharkhand", "Karnataka", "Kerala", "Ladakh", "Lakshadweep",
        "Madhya Pradesh", "Maharashtra", "Manipur", "Meghalaya", "Mizoram", "Nagaland", "Odisha", "Puducherry",
        "Punjab", "Rajasthan", "Sikkim", "Tamil Nadu", "Telangana", "Tripura", "Uttar Pradesh", "Uttarakhand",
        "West Bengal",
    ];

    private static readonly HashSet<string> StateSet = new(States, StringComparer.Ordinal);

    // Ten digits starting 6–9. The client strips "+91", spaces and a leading 0 before sending.
    [GeneratedRegex(@"^[6-9]\d{9}$")]
    private static partial Regex MobileRegex();

    // Six digits, first digit 1–9.
    [GeneratedRegex(@"^[1-9]\d{5}$")]
    private static partial Regex PincodeRegex();

    public static IRuleBuilderOptions<T, string?> IndianMobileNumber<T>(this IRuleBuilder<T, string?> rule) =>
        rule.NotEmpty().WithMessage("Enter your mobile number.")
            .Must(v => v is null || MobileRegex().IsMatch(v)).WithMessage("Enter a valid 10-digit mobile number.");

    public static IRuleBuilderOptions<T, string?> IndianPincode<T>(this IRuleBuilder<T, string?> rule) =>
        rule.NotEmpty().WithMessage("Enter the PIN code.")
            .Must(v => v is null || PincodeRegex().IsMatch(v)).WithMessage("Enter a valid 6-digit PIN code.");

    public static IRuleBuilderOptions<T, string?> IndianState<T>(this IRuleBuilder<T, string?> rule) =>
        rule.NotEmpty().WithMessage("Select a state.")
            .Must(v => v is null || StateSet.Contains(v)).WithMessage("Select a state from the list.");
}

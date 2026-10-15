using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Identity;

namespace GroceryStore.Api.Modules.Identity;

internal static class IdentityResultExtensions
{
    /// <summary>
    /// Turns Identity's errors into a 400 validation response keyed by form field, so the UI can show
    /// "Passwords must have at least 4 unique characters" under the right input.
    /// </summary>
    public static void ThrowIfFailed(this IdentityResult result, string passwordField = "password")
    {
        if (result.Succeeded)
        {
            return;
        }

        var failures = result.Errors.Select(e => new ValidationFailure(FieldFor(e.Code, passwordField), e.Description));
        throw new ValidationException(failures);
    }

    private static string FieldFor(string code, string passwordField) => code switch
    {
        _ when code.StartsWith("Password", StringComparison.Ordinal) => passwordField,
        _ when code.Contains("Email", StringComparison.Ordinal) || code.Contains("UserName", StringComparison.Ordinal) => "email",
        _ => string.Empty,
    };
}

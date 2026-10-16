using System.Globalization;
using System.Security.Claims;

namespace GroceryStore.Api.Common.Security;

public static class ClaimsPrincipalExtensions
{
    /// <summary>The signed-in user's id. Only call from endpoints protected by [Authorize].</summary>
    public static int GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("No authenticated user on this request.");
        return int.Parse(value, CultureInfo.InvariantCulture);
    }

    /// <summary>The user's id when signed in, otherwise null (for anonymous-friendly code such as rate limiting).</summary>
    public static int? GetUserIdOrNull(this ClaimsPrincipal principal) =>
        principal.Identity?.IsAuthenticated == true
        && int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), NumberStyles.None, CultureInfo.InvariantCulture, out var id)
            ? id
            : null;
}

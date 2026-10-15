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
}

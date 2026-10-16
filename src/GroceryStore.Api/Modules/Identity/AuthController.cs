using GroceryStore.Api.Common.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GroceryStore.Api.Modules.Identity;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController(AuthService auth) : ControllerBase
{
    /// <summary>Creates a customer account and signs the new customer in.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimits.Auth)]
    [ProducesResponseType<CurrentUserResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CurrentUserResponse>> Register(RegisterRequest request, CancellationToken ct)
    {
        var user = await auth.RegisterAsync(request, ct);
        return CreatedAtAction(nameof(Me), user);
    }

    /// <summary>Signs in with email or mobile number. Sets the HttpOnly auth cookie.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimits.Auth)]
    [ProducesResponseType<CurrentUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public Task<CurrentUserResponse> Login(LoginRequest request, CancellationToken ct) => auth.LoginAsync(request, ct);

    [HttpPost("logout")]
    [AllowAnonymous] // signing out with an expired cookie must still clear it
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout()
    {
        await auth.LogoutAsync();
        return NoContent();
    }

    /// <summary>
    /// The signed-in user, or 204 No Content for a visitor. The web app asks this on every start-up to
    /// restore the session; "nobody is signed in" is a normal answer, not an error, so it isn't a 401
    /// (which browsers log in red in the console on every page load).
    /// </summary>
    [HttpGet("me")]
    [AllowAnonymous]
    [ProducesResponseType<CurrentUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult<CurrentUserResponse>> Me()
    {
        if (User.GetUserIdOrNull() is not { } userId)
        {
            return NoContent();
        }

        // A valid cookie for an account that no longer exists also means "not signed in".
        return await auth.GetCurrentUserAsync(userId) is { } user ? user : NoContent();
    }
}

using GroceryStore.Api.Common.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GroceryStore.Api.Modules.Identity;

[ApiController]
[Route("api/account")]
[Authorize]
[Produces("application/json")]
public sealed class AccountController(AccountService account) : ControllerBase
{
    [HttpPut("profile")]
    [ProducesResponseType<CurrentUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<CurrentUserResponse> UpdateProfile(UpdateProfileRequest request, CancellationToken ct) =>
        account.UpdateProfileAsync(User.GetUserId(), request, ct);

    [HttpPost("change-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        await account.ChangePasswordAsync(User.GetUserId(), request);
        return NoContent();
    }
}

using GroceryStore.Api.Common.Errors;
using GroceryStore.Api.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GroceryStore.Api.Modules.Identity;

/// <summary>The signed-in user's own profile and password.</summary>
public sealed class AccountService(
    AppDbContext db,
    UserManager<AppUser> userManager,
    SignInManager<AppUser> signInManager,
    AuthService authService)
{
    public async Task<CurrentUserResponse> UpdateProfileAsync(int userId, UpdateProfileRequest request, CancellationToken ct)
    {
        var user = await FindAsync(userId);

        if (request.PhoneNumber != user.PhoneNumber &&
            await db.Users.AnyAsync(u => u.PhoneNumber == request.PhoneNumber && u.Id != userId, ct))
        {
            throw new ConflictException("This mobile number is already used by another account.");
        }

        user.FullName = request.FullName.Trim();
        user.PhoneNumber = request.PhoneNumber;
        (await userManager.UpdateAsync(user)).ThrowIfFailed();

        return await authService.ToResponseAsync(user);
    }

    public async Task ChangePasswordAsync(int userId, ChangePasswordRequest request)
    {
        var user = await FindAsync(userId);

        var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.PasswordMismatch)))
        {
            throw new FluentValidation.ValidationException(
                [new FluentValidation.Results.ValidationFailure("currentPassword", "The current password is incorrect.")]);
        }

        result.ThrowIfFailed(passwordField: "newPassword");

        // Changing the password rotates the security stamp. Other sessions (e.g. a phone left signed in)
        // are signed out the next time their cookie is re-validated (every 30 minutes by default).
        // Re-issue this session's cookie with the new stamp so the current session stays signed in.
        await signInManager.RefreshSignInAsync(user);
    }

    private async Task<AppUser> FindAsync(int userId) =>
        await userManager.FindByIdAsync(userId.ToString(System.Globalization.CultureInfo.InvariantCulture))
        ?? throw new NotFoundException("Account not found.");
}

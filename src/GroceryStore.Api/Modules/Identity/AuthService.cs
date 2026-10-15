using GroceryStore.Api.Common.Errors;
using GroceryStore.Api.Common.Validation;
using GroceryStore.Api.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GroceryStore.Api.Modules.Identity;

public sealed class AuthService(
    AppDbContext db,
    UserManager<AppUser> userManager,
    SignInManager<AppUser> signInManager,
    TimeProvider timeProvider)
{
    private const string InvalidCredentials = "The email/mobile number or password is incorrect.";

    public async Task<CurrentUserResponse> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim();

        // Friendly, field-specific messages for the common case; the unique indexes still guard races.
        if (await userManager.FindByEmailAsync(email) is not null)
        {
            throw new ConflictException("An account with this email already exists. Try signing in instead.");
        }

        if (await db.Users.AnyAsync(u => u.PhoneNumber == request.PhoneNumber, ct))
        {
            throw new ConflictException("An account with this mobile number already exists. Try signing in instead.");
        }

        var user = new AppUser
        {
            FullName = request.FullName.Trim(),
            Email = email,
            UserName = email,
            PhoneNumber = request.PhoneNumber,
            CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime,
        };

        // Creating the user and assigning the role must succeed or fail together.
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            (await userManager.CreateAsync(user, request.Password)).ThrowIfFailed();
            (await userManager.AddToRoleAsync(user, Roles.Customer)).ThrowIfFailed();
            await transaction.CommitAsync(ct);
        });

        await signInManager.SignInAsync(user, isPersistent: true);
        return await ToResponseAsync(user);
    }

    public async Task<CurrentUserResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var user = await FindByIdentifierAsync(request.Identifier.Trim(), ct)
            ?? throw new AuthenticationFailedException(InvalidCredentials);

        var result = await signInManager.PasswordSignInAsync(
            user, request.Password, request.RememberMe, lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            throw new AuthenticationFailedException(
                "Too many unsuccessful attempts. Your account is locked for 15 minutes.");
        }

        if (!result.Succeeded)
        {
            throw new AuthenticationFailedException(InvalidCredentials);
        }

        return await ToResponseAsync(user);
    }

    public Task LogoutAsync() => signInManager.SignOutAsync();

    public async Task<CurrentUserResponse?> GetCurrentUserAsync(int userId)
    {
        var user = await userManager.FindByIdAsync(userId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return user is null ? null : await ToResponseAsync(user);
    }

    private async Task<AppUser?> FindByIdentifierAsync(string identifier, CancellationToken ct)
    {
        if (identifier.Contains('@', StringComparison.Ordinal))
        {
            return await userManager.FindByEmailAsync(identifier);
        }

        var phone = PhoneNumbers.Normalize(identifier);
        return await db.Users.SingleOrDefaultAsync(u => u.PhoneNumber == phone, ct);
    }

    internal async Task<CurrentUserResponse> ToResponseAsync(AppUser user) =>
        new(user.Id, user.FullName, user.Email!, user.PhoneNumber, [.. await userManager.GetRolesAsync(user)]);
}

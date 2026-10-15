using FluentValidation;
using GroceryStore.Api.Common.Validation;

namespace GroceryStore.Api.Modules.Identity;

// ---------- Responses ----------

public sealed record CurrentUserResponse(
    int Id,
    string FullName,
    string Email,
    string? PhoneNumber,
    IReadOnlyList<string> Roles);

// ---------- Requests + validators ----------

public sealed record RegisterRequest(string FullName, string Email, string PhoneNumber, string Password);

internal sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(r => r.FullName).NotEmpty().WithMessage("Enter your name.").MaximumLength(100);
        RuleFor(r => r.Email).NotEmpty().WithMessage("Enter your email address.")
            .EmailAddress().WithMessage("Enter a valid email address.").MaximumLength(256);
        RuleFor(r => r.PhoneNumber).IndianMobileNumber();
        RuleFor(r => r.Password).NotEmpty().WithMessage("Choose a password.")
            .MinimumLength(8).WithMessage("Use at least 8 characters.").MaximumLength(100);
    }
}

/// <param name="Identifier">Email address or 10-digit mobile number.</param>
public sealed record LoginRequest(string Identifier, string Password, bool RememberMe = true);

internal sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(r => r.Identifier).NotEmpty().WithMessage("Enter your email or mobile number.");
        RuleFor(r => r.Password).NotEmpty().WithMessage("Enter your password.");
    }
}

public sealed record UpdateProfileRequest(string FullName, string PhoneNumber);

internal sealed class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(r => r.FullName).NotEmpty().WithMessage("Enter your name.").MaximumLength(100);
        RuleFor(r => r.PhoneNumber).IndianMobileNumber();
    }
}

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

internal sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(r => r.CurrentPassword).NotEmpty().WithMessage("Enter your current password.");
        RuleFor(r => r.NewPassword).NotEmpty().WithMessage("Choose a new password.")
            .MinimumLength(8).WithMessage("Use at least 8 characters.").MaximumLength(100)
            .NotEqual(r => r.CurrentPassword).WithMessage("The new password must be different.");
    }
}

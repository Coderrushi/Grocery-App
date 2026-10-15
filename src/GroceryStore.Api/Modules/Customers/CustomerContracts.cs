using FluentValidation;
using GroceryStore.Api.Common.Validation;

namespace GroceryStore.Api.Modules.Customers;

public sealed record AddressResponse(
    int Id,
    AddressLabel Label,
    string RecipientName,
    string PhoneNumber,
    string Line1,
    string? Line2,
    string? Landmark,
    string City,
    string State,
    string Pincode,
    bool IsDefault)
{
    internal static AddressResponse From(Address a) => new(
        a.Id, a.Label, a.RecipientName, a.PhoneNumber, a.Line1, a.Line2, a.Landmark, a.City, a.State, a.Pincode, a.IsDefault);
}

/// <summary>Used for both create and update.</summary>
public sealed record SaveAddressRequest(
    AddressLabel Label,
    string RecipientName,
    string PhoneNumber,
    string Line1,
    string? Line2,
    string? Landmark,
    string City,
    string State,
    string Pincode,
    bool IsDefault);

internal sealed class SaveAddressRequestValidator : AbstractValidator<SaveAddressRequest>
{
    public SaveAddressRequestValidator()
    {
        RuleFor(r => r.Label).IsInEnum();
        RuleFor(r => r.RecipientName).NotEmpty().WithMessage("Enter the recipient's name.").MaximumLength(100);
        RuleFor(r => r.PhoneNumber).IndianMobileNumber();
        RuleFor(r => r.Line1).NotEmpty().WithMessage("Enter the house/flat number and building.").MaximumLength(150);
        RuleFor(r => r.Line2).MaximumLength(150);
        RuleFor(r => r.Landmark).MaximumLength(100);
        RuleFor(r => r.City).NotEmpty().WithMessage("Enter the city.").MaximumLength(60);
        RuleFor(r => r.State).IndianState();
        RuleFor(r => r.Pincode).IndianPincode();
    }
}

public sealed record CustomerSummaryResponse(
    int Id,
    string FullName,
    string Email,
    string? PhoneNumber,
    DateTime RegisteredAtUtc,
    int AddressCount,
    decimal WalletBalance);

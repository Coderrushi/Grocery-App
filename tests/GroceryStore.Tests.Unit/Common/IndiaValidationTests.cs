using GroceryStore.Api.Common.Validation;
using GroceryStore.Api.Modules.Customers;
using GroceryStore.Api.Modules.Identity;

namespace GroceryStore.Tests.Unit.Common;

public sealed class IndiaValidationTests
{
    [Theory]
    [InlineData("9876543210", "9876543210")]
    [InlineData("+91 98765 43210", "9876543210")]
    [InlineData("+91-98765-43210", "9876543210")]
    [InlineData("09876543210", "9876543210")]
    [InlineData("919876543210", "9876543210")]
    [InlineData("98765", "98765")] // left for the validator to reject
    public void Phone_numbers_are_normalised_to_ten_digits(string input, string expected) =>
        Assert.Equal(expected, PhoneNumbers.Normalize(input));

    [Theory]
    [InlineData("9876543210", true)]
    [InlineData("6000000000", true)]
    [InlineData("5876543210", false)] // Indian mobiles start 6-9
    [InlineData("987654321", false)]
    [InlineData("98765432100", false)]
    [InlineData("+919876543210", false)] // must be normalised before sending
    public void Mobile_number_rule(string phone, bool valid)
    {
        var result = new RegisterRequestValidator().Validate(new RegisterRequest("Asha", "asha@example.com", phone, "long-enough"));
        Assert.Equal(valid, result.Errors.All(e => e.PropertyName != "PhoneNumber" && e.PropertyName != "phoneNumber"));
    }

    [Theory]
    [InlineData("411038", true)]
    [InlineData("110001", true)]
    [InlineData("011038", false)]
    [InlineData("41103", false)]
    [InlineData("41103A", false)]
    public void Pincode_rule(string pincode, bool valid)
    {
        var request = new SaveAddressRequest(AddressLabel.Home, "Asha", "9876543210", "Flat 1", null, null,
            "Pune", "Maharashtra", pincode, false);
        var result = new SaveAddressRequestValidator().Validate(request);
        Assert.Equal(valid, result.IsValid);
    }

    [Fact]
    public void State_must_be_from_the_list()
    {
        var request = new SaveAddressRequest(AddressLabel.Home, "Asha", "9876543210", "Flat 1", null, null,
            "Pune", "Bombay", "411038", false);
        Assert.False(new SaveAddressRequestValidator().Validate(request).IsValid);
        Assert.Equal(36, IndiaRules.States.Count);
    }
}

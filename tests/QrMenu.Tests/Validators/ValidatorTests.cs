using FluentAssertions;
using FluentValidation.TestHelper;
using QrMenu.Application.Auth;
using QrMenu.Application.Items;
using Xunit;

namespace QrMenu.Tests.Validators;

public class RegisterRequestValidatorTests
{
    private readonly RegisterRequestValidator _validator = new();

    [Fact]
    public void Fails_WhenWhatsAppNumberHasLetters()
    {
        var request = new RegisterRequest("Saket Rasoi", "Owner", "owner@test.com", "Password1", "98ABC54321");
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.WhatsAppNumber);
    }

    [Fact]
    public void Passes_WithValidTenDigitNumber()
    {
        var request = new RegisterRequest("Saket Rasoi", "Owner", "owner@test.com", "Password1", "9876543210");
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.WhatsAppNumber);
    }

    [Fact]
    public void Fails_WhenPasswordTooShort()
    {
        var request = new RegisterRequest("Saket Rasoi", "Owner", "owner@test.com", "abc", "9876543210");
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }
}

public class CreateItemRequestValidatorTests
{
    private readonly CreateItemRequestValidator _validator = new();

    [Fact]
    public void Fails_WhenPriceIsZeroOrNegative()
    {
        var request = new CreateItemRequest(Guid.NewGuid(), "Paneer Tikka", null, 0, null, true, null, null, null);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Price);
    }

    [Fact]
    public void Passes_WithValidItem()
    {
        var request = new CreateItemRequest(Guid.NewGuid(), "Paneer Tikka", "Grilled cottage cheese", 240, null, true, "Bestseller", null, null);
        var result = _validator.TestValidate(request);
        result.IsValid.Should().BeTrue();
    }
}

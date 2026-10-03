using FluentValidation;
using QrMenu.Application.Common;
using QrMenu.Domain.Entities;

namespace QrMenu.Application.Restaurants;

public class UpdateRestaurantRequestValidator : AbstractValidator<UpdateRestaurantRequest>
{
    public UpdateRestaurantRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Tagline).MaximumLength(300);
        RuleFor(x => x.Address).MaximumLength(500);
        RuleFor(x => x.Phone).MaximumLength(20);
        RuleFor(x => x.WhatsAppNumber).NotEmpty().Matches(@"^\+?[0-9]{10,15}$");
        RuleFor(x => x.OpenTime).NotEmpty().Matches(@"^\d{2}:\d{2}(:\d{2})?$");
        RuleFor(x => x.CloseTime).NotEmpty().Matches(@"^\d{2}:\d{2}(:\d{2})?$");
        RuleFor(x => x.GstPercentage).InclusiveBetween(0, 100);
        RuleFor(x => x.ServiceChargePercentage).InclusiveBetween(0, 100);
        RuleFor(x => x.WelcomeMessage).MaximumLength(300);
        RuleFor(x => x.LogoUrl).MustBeSafeImageUrl();
        RuleFor(x => x.ThemeColor)
            .Must(color => color is null || ThemeColors.IsValid(color))
            .WithMessage("Choose one of the available theme colours.");

        // 15-character GSTIN: 2-digit state code, PAN, entity number, "Z", check character.
        RuleFor(x => x.GstNumber)
            .Matches(@"^[0-9]{2}[A-Za-z]{5}[0-9]{4}[A-Za-z][1-9A-Za-z][Zz][0-9A-Za-z]$")
            .When(x => !string.IsNullOrWhiteSpace(x.GstNumber))
            .WithMessage("Enter a valid 15-character GSTIN, e.g. 07ABCDE1234F1Z5.");
        RuleFor(x => x.InvoicePrefix)
            .Matches(@"^[A-Za-z0-9]{1,10}$")
            .When(x => !string.IsNullOrWhiteSpace(x.InvoicePrefix))
            .WithMessage("Invoice prefix can only have letters and numbers (up to 10).");

        // UPI ids look like name@bank. No characters that could break out of the upi:// link.
        RuleFor(x => x.UpiId)
            .MaximumLength(100)
            .Matches(@"^[A-Za-z0-9._-]{2,256}@[A-Za-z][A-Za-z0-9]{1,63}$")
            .When(x => !string.IsNullOrWhiteSpace(x.UpiId))
            .WithMessage("Enter a valid UPI ID, e.g. saketrasoi@okicici.");
        RuleFor(x => x.UpiPayeeName)
            .NotEmpty().WithMessage("Enter the name customers will see when they pay.")
            .MaximumLength(100)
            .When(x => !string.IsNullOrWhiteSpace(x.UpiId));
    }
}

public class SetKitchenPinRequestValidator : AbstractValidator<SetKitchenPinRequest>
{
    public SetKitchenPinRequestValidator()
    {
        RuleFor(x => x.Pin)
            .Matches(@"^[0-9]{4,8}$")
            .When(x => !string.IsNullOrEmpty(x.Pin))
            .WithMessage("The kitchen PIN must be 4 to 8 digits.");
    }
}

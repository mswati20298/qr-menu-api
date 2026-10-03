using FluentValidation;

namespace QrMenu.Application.Kitchen;

public class KitchenLoginRequestValidator : AbstractValidator<KitchenLoginRequest>
{
    public KitchenLoginRequestValidator()
    {
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Pin).NotEmpty().MaximumLength(8);
    }
}

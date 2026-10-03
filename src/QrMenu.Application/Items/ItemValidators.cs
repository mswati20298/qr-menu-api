using FluentValidation;
using QrMenu.Application.Common;

namespace QrMenu.Application.Items;

public class VariantInputValidator : AbstractValidator<VariantInput>
{
    public VariantInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Price).GreaterThan(0);
    }
}

public class AddOnInputValidator : AbstractValidator<AddOnInput>
{
    public AddOnInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
    }
}

public class CreateItemRequestValidator : AbstractValidator<CreateItemRequest>
{
    public CreateItemRequestValidator()
    {
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.Price).GreaterThan(0);
        RuleFor(x => x.ImageUrl).MustBeSafeImageUrl();
        RuleFor(x => x.Tag).MaximumLength(50);
        RuleForEach(x => x.Variants).SetValidator(new VariantInputValidator());
        RuleForEach(x => x.AddOns).SetValidator(new AddOnInputValidator());
    }
}

public class UpdateItemRequestValidator : AbstractValidator<UpdateItemRequest>
{
    public UpdateItemRequestValidator()
    {
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.Price).GreaterThan(0);
        RuleFor(x => x.ImageUrl).MustBeSafeImageUrl();
        RuleFor(x => x.Tag).MaximumLength(50);
        RuleForEach(x => x.Variants).SetValidator(new VariantInputValidator());
        RuleForEach(x => x.AddOns).SetValidator(new AddOnInputValidator());
    }
}

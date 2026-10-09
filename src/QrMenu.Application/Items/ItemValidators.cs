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

/// <summary>Rules for a dish, shared by create and update.</summary>
public abstract class ItemInputValidator<T> : AbstractValidator<T> where T : IItemInput
{
    protected ItemInputValidator()
    {
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.Price).GreaterThan(0);
        RuleFor(x => x.ImageUrl).MustBeSafeImageUrl();
        RuleFor(x => x.Tag).MaximumLength(50);
        RuleForEach(x => x.Variants).SetValidator(new VariantInputValidator());
        RuleForEach(x => x.AddOns).SetValidator(new AddOnInputValidator());

        // "Half" twice, or the same add-on twice, on one dish makes no sense and confuses the guest.
        RuleFor(x => x.Variants).Must(v => NamesAreUnique(v?.Select(i => i.Name)))
            .WithMessage("Each size needs a different name.");
        RuleFor(x => x.AddOns).Must(a => NamesAreUnique(a?.Select(i => i.Name)))
            .WithMessage("Each add-on needs a different name.");
    }

    private static bool NamesAreUnique(IEnumerable<string>? names)
    {
        var list = names?.Select(NameRules.Normalize).Where(n => n.Length > 0).ToList() ?? [];
        return list.Distinct(StringComparer.OrdinalIgnoreCase).Count() == list.Count;
    }
}

public class CreateItemRequestValidator : ItemInputValidator<CreateItemRequest>;

public class UpdateItemRequestValidator : ItemInputValidator<UpdateItemRequest>;

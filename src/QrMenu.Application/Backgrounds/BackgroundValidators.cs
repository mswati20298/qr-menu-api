using FluentValidation;
using QrMenu.Application.Common;

namespace QrMenu.Application.Backgrounds;

public class AddBackgroundRequestValidator : AbstractValidator<AddBackgroundRequest>
{
    public AddBackgroundRequestValidator()
    {
        RuleFor(x => x.ImageUrl).NotEmpty().MaximumLength(500).MustBeSafeImageUrl();
    }
}

public class UpdateBackgroundRequestValidator : AbstractValidator<UpdateBackgroundRequest>
{
    public UpdateBackgroundRequestValidator()
    {
        // 4 slots => valid bit masks are 0..15.
        RuleFor(x => x.Slots).InclusiveBetween(0, 15);
    }
}

public class SetBackgroundModeRequestValidator : AbstractValidator<SetBackgroundModeRequest>
{
    public SetBackgroundModeRequestValidator()
    {
        RuleFor(x => x.Mode)
            .Must(mode => mode == "Fixed" || mode == "TimeOfDay")
            .WithMessage("Mode must be 'Fixed' or 'TimeOfDay'.");
    }
}

using FluentValidation;

namespace QrMenu.Application.ServiceRequests;

public class CreateServiceRequestValidator : AbstractValidator<CreateServiceRequest>
{
    public CreateServiceRequestValidator()
    {
        RuleFor(x => x.TableNumber).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Type)
            .Must(type => type == "CallWaiter" || type == "Water" || type == "Bill")
            .WithMessage("Type must be 'CallWaiter', 'Water' or 'Bill'.");
    }
}

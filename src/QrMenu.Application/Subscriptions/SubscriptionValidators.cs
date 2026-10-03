using FluentValidation;

namespace QrMenu.Application.Subscriptions;

public class GrantFreePlanRequestValidator : AbstractValidator<GrantFreePlanRequest>
{
    public GrantFreePlanRequestValidator()
    {
        RuleFor(x => x.Until)
            .NotNull().WithMessage("Choose the date the free plan ends, or make it lifetime.")
            .Must(until => until > DateTime.UtcNow).WithMessage("The end date must be in the future.")
            .When(x => !x.Lifetime);
        RuleFor(x => x.Note).MaximumLength(500);
    }
}

public class RecordPaymentRequestValidator : AbstractValidator<RecordPaymentRequest>
{
    // "Online" is only set by the payment gateway, never typed in by hand.
    private static readonly string[] Methods = ["Cash", "Upi", "BankTransfer", "Card", "Other"];

    public RecordPaymentRequestValidator()
    {
        RuleFor(x => x.PricingPlanId).NotEmpty().WithMessage("Choose a plan.");
        RuleFor(x => x.Periods).InclusiveBetween(1, 36);
        RuleFor(x => x.Amount).GreaterThan(0).LessThanOrEqualTo(10_000_000);
        RuleFor(x => x.PaymentMethod)
            .Must(method => Methods.Contains(method))
            .WithMessage("Payment method must be Cash, Upi, BankTransfer, Card or Other.");
        RuleFor(x => x.PaymentReference).MaximumLength(100);
        RuleFor(x => x.Note).MaximumLength(500);
    }
}

public class ExtendPlanRequestValidator : AbstractValidator<ExtendPlanRequest>
{
    public ExtendPlanRequestValidator()
    {
        RuleFor(x => x.Until).Must(until => until > DateTime.UtcNow).WithMessage("The new end date must be in the future.");
        RuleFor(x => x.Note).MaximumLength(500);
    }
}

public class CancelPlanRequestValidator : AbstractValidator<CancelPlanRequest>
{
    public CancelPlanRequestValidator()
    {
        RuleFor(x => x.Note).MaximumLength(500);
    }
}

public class SavePricingPlanRequestValidator : AbstractValidator<SavePricingPlanRequest>
{
    public SavePricingPlanRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.DurationMonths).InclusiveBetween(1, 60).WithMessage("Duration must be between 1 and 60 months.");
        // Razorpay's smallest charge is ₹1.
        RuleFor(x => x.Price).GreaterThanOrEqualTo(1).LessThanOrEqualTo(10_000_000)
            .Must(price => decimal.Round(price, 2) == price).WithMessage("Price can have at most 2 decimal places.");
        RuleFor(x => x.SortOrder).InclusiveBetween(0, 1000);
    }
}

public class StartCheckoutRequestValidator : AbstractValidator<StartCheckoutRequest>
{
    public StartCheckoutRequestValidator()
    {
        RuleFor(x => x.PricingPlanId).NotEmpty();
    }
}

public class ConfirmCheckoutRequestValidator : AbstractValidator<ConfirmCheckoutRequest>
{
    public ConfirmCheckoutRequestValidator()
    {
        RuleFor(x => x.RazorpayOrderId).NotEmpty().MaximumLength(100);
        RuleFor(x => x.RazorpayPaymentId).NotEmpty().MaximumLength(100);
        RuleFor(x => x.RazorpaySignature).NotEmpty().MaximumLength(200);
    }
}

using FluentValidation;

namespace QrMenu.Application.Orders;

public class OrderItemInputValidator : AbstractValidator<OrderItemInput>
{
    public OrderItemInputValidator()
    {
        RuleFor(x => x.MenuItemId).NotEmpty();
        RuleFor(x => x.Qty).GreaterThan(0).LessThanOrEqualTo(50);
    }
}

public class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderRequestValidator()
    {
        RuleFor(x => x.CustomerName).MaximumLength(200);
        RuleFor(x => x.CustomerPhone).Matches(@"^\+?[0-9]{10,15}$").When(x => !string.IsNullOrWhiteSpace(x.CustomerPhone));
        RuleFor(x => x.TableNumber).MaximumLength(20);
        RuleFor(x => x.Items).NotEmpty().WithMessage("Order must contain at least one item.");
        RuleForEach(x => x.Items).SetValidator(new OrderItemInputValidator());
    }
}

public class UpdateOrderStatusRequestValidator : AbstractValidator<UpdateOrderStatusRequest>
{
    private static readonly string[] ValidStatuses = ["Placed", "Preparing", "Served", "Completed", "Cancelled"];

    public UpdateOrderStatusRequestValidator()
    {
        RuleFor(x => x.Status).NotEmpty().Must(s => ValidStatuses.Contains(s))
            .WithMessage($"Status must be one of: {string.Join(", ", ValidStatuses)}.");
    }
}

public class ClaimPaymentRequestValidator : AbstractValidator<ClaimPaymentRequest>
{
    public ClaimPaymentRequestValidator()
    {
        // UTRs are 12 digits, but apps show other ids too; keep it to plain letters and numbers.
        RuleFor(x => x.Reference)
            .MaximumLength(50)
            .Matches(@"^[A-Za-z0-9\-]*$").WithMessage("The transaction id can only have letters and numbers.");
    }
}

public class UpdatePaymentRequestValidator : AbstractValidator<UpdatePaymentRequest>
{
    private static readonly string[] Methods = ["Cash", "Upi", "Card", "Other"];

    public UpdatePaymentRequestValidator()
    {
        RuleFor(x => x.Status).Must(s => s is "Paid" or "Unpaid").WithMessage("Status must be Paid or Unpaid.");
        RuleFor(x => x.Method)
            .Must(m => m is not null && Methods.Contains(m))
            .When(x => x.Status == "Paid")
            .WithMessage("Choose how it was paid: Cash, Upi, Card or Other.");
        RuleFor(x => x.Reference).MaximumLength(50);
    }
}

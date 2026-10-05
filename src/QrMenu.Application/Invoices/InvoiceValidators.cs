using FluentValidation;

namespace QrMenu.Application.Invoices;

public class CreateInvoiceRequestValidator : AbstractValidator<CreateInvoiceRequest>
{
    public CreateInvoiceRequestValidator()
    {
        RuleFor(x => x)
            .Must(x => x.OrderId.HasValue != !string.IsNullOrWhiteSpace(x.TableNumber))
            .WithMessage("Choose either an order or a table to bill.");
        RuleFor(x => x.TableNumber).MaximumLength(20);
    }
}

public class MarkInvoicePaidRequestValidator : AbstractValidator<MarkInvoicePaidRequest>
{
    private static readonly string[] Methods = ["Cash", "Upi", "Card", "Other"];

    public MarkInvoicePaidRequestValidator()
    {
        RuleFor(x => x.Method).Must(m => Methods.Contains(m)).WithMessage("Choose how it was paid: Cash, Upi, Card or Other.");
        RuleFor(x => x.Reference).MaximumLength(50);
    }
}

public class ManualInvoiceRequestValidator : AbstractValidator<ManualInvoiceRequest>
{
    private static readonly string[] Methods = ["Cash", "Upi", "Card", "Other"];

    public ManualInvoiceRequestValidator()
    {
        RuleFor(x => x.CustomerName).MaximumLength(200);
        RuleFor(x => x.CustomerPhone).Matches(@"^\+?[0-9]{10,15}$").When(x => !string.IsNullOrWhiteSpace(x.CustomerPhone))
            .WithMessage("Enter a valid phone number, or leave it empty.");
        RuleFor(x => x.TableNumber).MaximumLength(20);
        RuleFor(x => x.Note).MaximumLength(500);
        RuleFor(x => x.Items).NotEmpty().WithMessage("Add at least one item.");
        RuleFor(x => x.Items.Count).LessThanOrEqualTo(100).WithMessage("A bill can have at most 100 lines.");
        RuleForEach(x => x.Items).SetValidator(new QrMenu.Application.Orders.OrderItemInputValidator());
        RuleFor(x => x.PaidWith).Must(m => m is null || Methods.Contains(m))
            .WithMessage("Paid with must be Cash, Upi, Card or Other.");
    }
}

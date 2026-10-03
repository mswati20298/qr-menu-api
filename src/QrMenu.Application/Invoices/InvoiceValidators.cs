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

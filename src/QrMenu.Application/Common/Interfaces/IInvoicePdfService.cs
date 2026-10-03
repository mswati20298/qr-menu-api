using QrMenu.Application.Invoices;

namespace QrMenu.Application.Common.Interfaces;

public interface IInvoicePdfService
{
    /// <summary>receipt = 80 mm thermal printer roll; otherwise an A4 page.</summary>
    byte[] Generate(InvoiceDto invoice, bool receipt);
}

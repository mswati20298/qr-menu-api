namespace QrMenu.Application.Invoices;

public interface IInvoiceService
{
    /// <summary>Bills one order or a whole table. Billing an order that is already on an invoice returns that invoice.</summary>
    Task<InvoiceDto> CreateAsync(Guid restaurantId, CreateInvoiceRequest request, CancellationToken ct = default);
    Task<InvoiceDto> GetAsync(Guid restaurantId, Guid invoiceId, CancellationToken ct = default);
    Task<InvoicePageDto> ListAsync(Guid restaurantId, string? search, int page, int pageSize, CancellationToken ct = default);

    /// <summary>Every bill made in the period (UTC), oldest first, for the GST / sales report.</summary>
    Task<List<InvoiceExportRowDto>> ExportAsync(Guid restaurantId, DateTime from, DateTime to, CancellationToken ct = default);
    Task<InvoiceDto> MarkPaidAsync(Guid restaurantId, Guid invoiceId, MarkInvoicePaidRequest request, CancellationToken ct = default);
    Task<(byte[] Pdf, string Number)> GetPdfAsync(Guid restaurantId, Guid invoiceId, bool receipt, CancellationToken ct = default);
}

using Microsoft.AspNetCore.Mvc;
using QrMenu.Application.Invoices;

namespace QrMenu.Api.Controllers;

[Route("api/invoices")]
public class InvoicesController(IInvoiceService invoiceService) : OwnerControllerBase
{
    [HttpGet]
    public async Task<ActionResult<InvoicePageDto>> List(
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        return Ok(await invoiceService.ListAsync(RestaurantId, search, page, pageSize, ct));
    }

    [HttpPost]
    public async Task<ActionResult<InvoiceDto>> Create(CreateInvoiceRequest request, CancellationToken ct)
    {
        return Ok(await invoiceService.CreateAsync(RestaurantId, request, ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<InvoiceDto>> Get(Guid id, CancellationToken ct)
    {
        return Ok(await invoiceService.GetAsync(RestaurantId, id, ct));
    }

    /// <summary>format: "receipt" (80 mm thermal printer, default) or "a4".</summary>
    [HttpGet("{id:guid}/pdf")]
    public async Task<IActionResult> Pdf(Guid id, [FromQuery] string? format, CancellationToken ct)
    {
        var receipt = !string.Equals(format, "a4", StringComparison.OrdinalIgnoreCase);
        var (pdf, number) = await invoiceService.GetPdfAsync(RestaurantId, id, receipt, ct);
        return File(pdf, "application/pdf", $"{number}.pdf");
    }

    [HttpPost("{id:guid}/mark-paid")]
    public async Task<ActionResult<InvoiceDto>> MarkPaid(Guid id, MarkInvoicePaidRequest request, CancellationToken ct)
    {
        return Ok(await invoiceService.MarkPaidAsync(RestaurantId, id, request, ct));
    }
}

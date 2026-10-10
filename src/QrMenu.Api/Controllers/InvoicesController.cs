using Microsoft.AspNetCore.Mvc;
using QrMenu.Application.Invoices;
using QrMenu.Application.Orders;

namespace QrMenu.Api.Controllers;

[Route("api/invoices")]
public class InvoicesController(IInvoiceService invoiceService, IOrderService orderService) : OwnerControllerBase
{
    /// <summary>
    /// Counter bill: an order entered by staff, billed at once (and marked paid when PaidWith is given).
    /// The order goes to the kitchen screen only when SendToKitchen is true.
    /// </summary>
    [HttpPost("manual")]
    public async Task<ActionResult<InvoiceDto>> CreateManual(ManualInvoiceRequest request, CancellationToken ct)
    {
        var order = await orderService.CreateStaffOrderAsync(RestaurantId, new StaffOrderRequest(
            request.TableNumber, request.CustomerName, request.CustomerPhone, request.Note,
            request.SkipServiceCharge, request.SendToKitchen, request.Items), ct);

        var invoice = await invoiceService.CreateAsync(RestaurantId, new CreateInvoiceRequest(order.Id, null), ct);

        if (request.PaidWith is not null)
        {
            invoice = await invoiceService.MarkPaidAsync(RestaurantId, invoice.Id, new MarkInvoicePaidRequest(request.PaidWith, null), ct);
        }

        return Ok(invoice);
    }

    [HttpGet]
    public async Task<ActionResult<InvoicePageDto>> List(
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        return Ok(await invoiceService.ListAsync(RestaurantId, search, page, pageSize, ct));
    }

    /// <summary>GST / sales report: every bill in the period (UTC).</summary>
    [HttpGet("export")]
    public async Task<ActionResult<List<InvoiceExportRowDto>>> Export([FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken ct)
    {
        return Ok(await invoiceService.ExportAsync(RestaurantId, from.ToUniversalTime(), to.ToUniversalTime(), ct));
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

using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using QrMenu.Application.Common.Exceptions;
using QrMenu.Application.Orders;
using QrMenu.Application.PublicMenu;
using QrMenu.Application.ServiceRequests;

namespace QrMenu.Api.Controllers;

[ApiController]
[Route("api/public")]
public class PublicMenuController(
    IPublicMenuService publicMenuService,
    IOrderService orderService,
    IServiceRequestService serviceRequestService) : ControllerBase
{
    [HttpGet("{slug}/menu")]
    public async Task<ActionResult<PublicMenuResponse>> GetMenu(string slug, CancellationToken ct)
    {
        var result = await publicMenuService.GetMenuAsync(slug, ct);
        return Ok(result);
    }

    [EnableRateLimiting(RateLimits.Public)]
    [HttpPost("{slug}/scan")]
    public async Task<IActionResult> Scan(string slug, [FromQuery] string? table, CancellationToken ct)
    {
        await publicMenuService.LogScanAsync(slug, table, ct);
        return NoContent();
    }

    /// <summary>Called when the menu is opened from a table's QR: checks its code, returns a table session.</summary>
    [EnableRateLimiting(RateLimits.Public)]
    [HttpPost("{slug}/table-session")]
    public async Task<ActionResult<TableSessionDto>> StartTableSession(string slug, StartTableSessionRequest request, CancellationToken ct)
    {
        return Ok(await publicMenuService.StartTableSessionAsync(slug, request, ct));
    }

    [EnableRateLimiting(RateLimits.Public)]
    [HttpPost("{slug}/requests")]
    public async Task<ActionResult<ServiceRequestDto>> RaiseServiceRequest(string slug, CreateServiceRequest request, CancellationToken ct)
    {
        var result = await serviceRequestService.CreatePublicAsync(slug, request, ct);
        return Ok(result);
    }

    [EnableRateLimiting(RateLimits.Public)]
    [HttpPost("{slug}/orders")]
    public async Task<ActionResult<OrderDto>> CreateOrder(string slug, CreateOrderRequest request, CancellationToken ct)
    {
        var result = await orderService.CreatePublicOrderAsync(slug, request, ct);
        return Ok(result);
    }

    [EnableRateLimiting(RateLimits.Public)]
    [HttpGet("{slug}/orders")]
    public async Task<ActionResult<List<OrderDto>>> GetOrdersByPhone(string slug, [FromQuery] string phone, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            throw new ConflictException("A phone number is required to look up orders.");
        }

        var result = await orderService.GetOrdersByPhoneAsync(slug, phone, ct);
        return Ok(result);
    }

    [HttpGet("{slug}/orders/{orderId:guid}")]
    public async Task<ActionResult<OrderDto>> GetOrder(string slug, Guid orderId, CancellationToken ct)
    {
        var result = await orderService.GetPublicOrderAsync(slug, orderId, ct);
        return Ok(result);
    }

    /// <summary>The customer says they paid this order by UPI. Staff still has to confirm it.</summary>
    [EnableRateLimiting(RateLimits.Public)]
    [HttpPost("{slug}/orders/{orderId:guid}/payment-claim")]
    public async Task<ActionResult<OrderDto>> ClaimPayment(string slug, Guid orderId, ClaimPaymentRequest request, CancellationToken ct)
    {
        var result = await orderService.ClaimPublicPaymentAsync(slug, orderId, request, ct);
        return Ok(result);
    }

    [EnableRateLimiting(RateLimits.Public)]
    [HttpPatch("{slug}/orders/{orderId:guid}/cancel")]
    public async Task<ActionResult<OrderDto>> CancelOrder(string slug, Guid orderId, CancellationToken ct)
    {
        var result = await orderService.CancelPublicOrderAsync(slug, orderId, ct);
        return Ok(result);
    }
}

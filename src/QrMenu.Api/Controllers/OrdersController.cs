using Microsoft.AspNetCore.Mvc;
using QrMenu.Application.Orders;

namespace QrMenu.Api.Controllers;

[Route("api/orders")]
public class OrdersController(IOrderService orderService) : OwnerControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<OrderDto>>> GetAll(
        [FromQuery] string? status, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var result = await orderService.GetAllForOwnerAsync(RestaurantId, status, from?.ToUniversalTime(), to?.ToUniversalTime(), ct);
        return Ok(result);
    }

    // "since" arrives as a UTC ISO string; ASP.NET binds it as server-local time, while
    // Order.CreatedAt is stored in UTC. Normalise back to UTC before querying.
    [HttpGet("notifications")]
    public async Task<ActionResult<List<OrderDto>>> GetNewSince([FromQuery] DateTime since, CancellationToken ct)
    {
        var result = await orderService.GetNewOrdersSinceAsync(RestaurantId, since.ToUniversalTime(), ct);
        return Ok(result);
    }

    /// <summary>Staff adds an order (to a table or as takeaway) without billing it yet.</summary>
    [HttpPost]
    public async Task<ActionResult<OrderDto>> CreateStaffOrder(StaffOrderRequest request, CancellationToken ct)
    {
        var result = await orderService.CreateStaffOrderAsync(RestaurantId, request, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrderDto>> Get(Guid id, CancellationToken ct)
    {
        var result = await orderService.GetForOwnerAsync(RestaurantId, id, ct);
        return Ok(result);
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<OrderDto>> UpdateStatus(Guid id, UpdateOrderStatusRequest request, CancellationToken ct)
    {
        var result = await orderService.UpdateStatusAsync(RestaurantId, id, request.Status, ct);
        return Ok(result);
    }

    /// <summary>Staff confirms a payment (or rejects a customer's UPI claim).</summary>
    [HttpPatch("{id:guid}/payment")]
    public async Task<ActionResult<OrderDto>> UpdatePayment(Guid id, UpdatePaymentRequest request, CancellationToken ct)
    {
        var result = await orderService.UpdatePaymentAsync(RestaurantId, id, request, ct);
        return Ok(result);
    }
}

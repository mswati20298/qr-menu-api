using Microsoft.AspNetCore.Mvc;
using QrMenu.Application.ServiceRequests;

namespace QrMenu.Api.Controllers;

[Route("api/requests")]
public class ServiceRequestsController(IServiceRequestService serviceRequestService) : OwnerControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ServiceRequestDto>>> GetPending(CancellationToken ct)
    {
        var result = await serviceRequestService.GetPendingAsync(RestaurantId, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/complete")]
    public async Task<IActionResult> Complete(Guid id, CancellationToken ct)
    {
        await serviceRequestService.CompleteAsync(RestaurantId, id, ct);
        return NoContent();
    }
}

using Microsoft.AspNetCore.Mvc;
using QrMenu.Application.Feedbacks;

namespace QrMenu.Api.Controllers;

/// <summary>What guests said about this restaurant, and the owner's own rating of QRenvo.</summary>
[Route("api/restaurant/feedback")]
public class RestaurantFeedbackController(IFeedbackService feedbackService) : OwnerControllerBase
{
    [HttpGet("customers")]
    public async Task<ActionResult<CustomerFeedbackSummaryDto>> Customers(CancellationToken ct)
    {
        return Ok(await feedbackService.GetCustomerFeedbackAsync(RestaurantId, ct));
    }

    [HttpGet("mine")]
    public async Task<ActionResult<FeedbackDto>> Mine(CancellationToken ct)
    {
        var feedback = await feedbackService.GetOwnerFeedbackAsync(RestaurantId, ct);
        return feedback is null ? NoContent() : Ok(feedback);
    }

    [HttpPut("mine")]
    public async Task<ActionResult<FeedbackDto>> SaveMine(SubmitOwnerFeedbackRequest request, CancellationToken ct)
    {
        return Ok(await feedbackService.SaveOwnerFeedbackAsync(RestaurantId, request, ct));
    }
}

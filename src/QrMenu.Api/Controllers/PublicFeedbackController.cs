using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using QrMenu.Application.Common.Interfaces;
using QrMenu.Application.Feedbacks;

namespace QrMenu.Api.Controllers;

/// <summary>Guests rate their order (no login: the order id is only known to the phone that placed it).</summary>
[ApiController]
[Route("api/public")]
public class PublicFeedbackController(
    IFeedbackService feedbackService,
    IFileStorageService fileStorage,
    IImageOptimizer imageOptimizer) : ControllerBase
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp"
    };

    private const long MaxFileSizeBytes = 10 * 1024 * 1024;

    [HttpGet("{slug}/orders/{orderId:guid}/feedback")]
    public async Task<ActionResult<FeedbackDto>> Get(string slug, Guid orderId, CancellationToken ct)
    {
        var feedback = await feedbackService.GetForOrderAsync(slug, orderId, ct);
        return feedback is null ? NoContent() : Ok(feedback);
    }

    [EnableRateLimiting(RateLimits.Public)]
    [HttpPost("{slug}/orders/{orderId:guid}/feedback")]
    public async Task<ActionResult<FeedbackDto>> Submit(string slug, Guid orderId, SubmitCustomerFeedbackRequest request, CancellationToken ct)
    {
        return Ok(await feedbackService.SubmitForOrderAsync(slug, orderId, request, ct));
    }

    /// <summary>Optional photo with the rating. Only for a real order of this restaurant, and rate limited.</summary>
    [EnableRateLimiting(RateLimits.Public)]
    [HttpPost("{slug}/orders/{orderId:guid}/feedback/image")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    public async Task<IActionResult> UploadImage(string slug, Guid orderId, IFormFile file, CancellationToken ct)
    {
        if (file.Length == 0 || file.Length > MaxFileSizeBytes)
        {
            return BadRequest(new { message = "Please choose a photo under 10 MB." });
        }
        if (!AllowedContentTypes.Contains(file.ContentType))
        {
            return BadRequest(new { message = "Only JPEG, PNG or WEBP photos are allowed." });
        }

        await feedbackService.EnsureOrderExistsAsync(slug, orderId, ct);

        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream, ct);
        stream.Position = 0;
        var image = await imageOptimizer.OptimizeAsync(stream, ImagePurpose.Feedback, ct);
        var url = await fileStorage.SaveAsync(image.Content, image.Extension, ct);
        return Ok(new { url });
    }

    /// <summary>Ratings the super admin published, for the QRenvo landing page (another host, hence open CORS).</summary>
    [EnableCors(PublicReadCors.Policy)]
    [HttpGet("testimonials")]
    public async Task<ActionResult<List<TestimonialDto>>> Testimonials(CancellationToken ct)
    {
        Response.Headers.CacheControl = "public, max-age=300";
        return Ok(await feedbackService.GetTestimonialsAsync(ct));
    }
}

public static class PublicReadCors
{
    /// <summary>Any site may read (GET only); used for the landing page testimonials.</summary>
    public const string Policy = "PublicRead";
}

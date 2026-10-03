using Microsoft.AspNetCore.Mvc;
using QrMenu.Application.Subscriptions;

namespace QrMenu.Api.Controllers;

/// <summary>
/// Called by Razorpay, not by a user, so there is no login: the request is trusted only when its
/// X-Razorpay-Signature matches the webhook secret.
/// </summary>
[ApiController]
[Route("api/payments")]
public class PaymentWebhooksController(IOnlinePaymentService onlinePaymentService) : ControllerBase
{
    [HttpPost("razorpay/webhook")]
    public async Task<IActionResult> Razorpay(CancellationToken ct)
    {
        // The signature covers the exact bytes Razorpay sent, so read the raw body.
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync(ct);

        var accepted = await onlinePaymentService.HandleWebhookAsync(body, Request.Headers["X-Razorpay-Signature"], ct);
        return accepted ? Ok() : BadRequest();
    }
}

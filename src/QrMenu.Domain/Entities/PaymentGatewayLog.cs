namespace QrMenu.Domain.Entities;

/// <summary>
/// One message to or from the payment gateway (Razorpay), kept for the super admin's payment log:
/// the order request and response, the checkout confirmation, every webhook and what was done with it.
/// Never holds the key secret or the Authorization header.
/// </summary>
public class PaymentGatewayLog
{
    public Guid Id { get; set; }

    /// <summary>Razorpay order id (order_...), when known; links the entries of one checkout together.</summary>
    public string? GatewayOrderId { get; set; }

    /// <summary>"order.create", "checkout.confirm", "webhook" or "result".</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>HTTP status of Razorpay's answer (order.create) or of our answer (webhook).</summary>
    public int? StatusCode { get; set; }

    public string? RequestBody { get; set; }
    public string? ResponseBody { get; set; }

    /// <summary>Short plain-language outcome, e.g. "Signature valid", "Plan applied", "Amount mismatch".</summary>
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

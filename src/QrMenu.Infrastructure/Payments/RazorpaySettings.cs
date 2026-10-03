namespace QrMenu.Infrastructure.Payments;

/// <summary>
/// Bound from the "Razorpay" config section. Keep the secrets in user-secrets or environment variables,
/// never in source control. Use the rzp_test_ keys while testing.
/// </summary>
public class RazorpaySettings
{
    public string KeyId { get; set; } = string.Empty;
    public string KeySecret { get; set; } = string.Empty;

    /// <summary>Secret set on the webhook in the Razorpay dashboard. Optional; without it the webhook is refused.</summary>
    public string WebhookSecret { get; set; } = string.Empty;
}

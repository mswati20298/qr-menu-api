namespace QrMenu.Domain.Entities;

/// <summary>Platform-wide settings the super admin can change from the panel. There is exactly one row (Id = 1).</summary>
public class PlatformSettings
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;

    /// <summary>
    /// Free trial for a newly registered restaurant, in days. 0 = no trial: the restaurant has to buy a plan
    /// before customers can order. Changing it only affects restaurants that register afterwards.
    /// </summary>
    public int TrialDays { get; set; } = 3;

    // Payment and AI keys entered in the super admin panel. Empty = the server configuration value is used.
    // The secrets are stored encrypted; the Razorpay key id is public (it goes to the browser anyway).
    public string? RazorpayKeyId { get; set; }
    public string? RazorpayKeySecretEncrypted { get; set; }
    public string? RazorpayWebhookSecretEncrypted { get; set; }
    public string? GeminiApiKeyEncrypted { get; set; }

    /// <summary>Demo deployment only: wipe and re-seed the sample data every this many days. 0 = only by hand.</summary>
    public int DemoAutoResetDays { get; set; } = 30;

    public DateTime? LastDemoResetAt { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Email of the super admin who last changed the settings.</summary>
    public string? UpdatedBy { get; set; }
}

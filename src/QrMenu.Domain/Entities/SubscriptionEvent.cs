namespace QrMenu.Domain.Entities;

/// <summary>
/// One line of a restaurant's plan history: every change the super admin makes (or the sign-up trial)
/// is recorded here, together with the payment details when money was received.
/// </summary>
public class SubscriptionEvent
{
    public Guid Id { get; set; }
    public Guid RestaurantId { get; set; }
    public SubscriptionAction Action { get; set; }

    /// <summary>The plan after this change.</summary>
    public SubscriptionPlan Plan { get; set; }
    public string? PlanName { get; set; }

    /// <summary>The plan's end after this change. Null = no end date (lifetime free).</summary>
    public DateTime? ExpiresAt { get; set; }

    public decimal? Amount { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    public string? PaymentReference { get; set; }

    /// <summary>Internal note for the super admin. Not shown to the owner.</summary>
    public string? Note { get; set; }

    /// <summary>Email of the super admin who made the change, or "system".</summary>
    public string PerformedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Restaurant Restaurant { get; set; } = null!;
}

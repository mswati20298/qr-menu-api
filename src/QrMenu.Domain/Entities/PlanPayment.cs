namespace QrMenu.Domain.Entities;

public enum PlanPaymentStatus
{
    Created,
    Paid
}

/// <summary>
/// One online checkout by an owner. Created when the owner clicks "Pay"; marked Paid (and the plan applied)
/// once the gateway confirms the payment, either from the browser callback or the webhook — whichever comes first.
/// </summary>
public class PlanPayment
{
    public Guid Id { get; set; }
    public Guid RestaurantId { get; set; }
    public Guid PricingPlanId { get; set; }

    // Snapshot of the plan at checkout time, so a later price change does not affect this payment.
    public string PlanName { get; set; } = string.Empty;
    public int DurationMonths { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "INR";

    public PlanPaymentStatus Status { get; set; } = PlanPaymentStatus.Created;
    public string GatewayOrderId { get; set; } = string.Empty;
    public string? GatewayPaymentId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAt { get; set; }

    public Restaurant Restaurant { get; set; } = null!;
    public PricingPlan PricingPlan { get; set; } = null!;
}

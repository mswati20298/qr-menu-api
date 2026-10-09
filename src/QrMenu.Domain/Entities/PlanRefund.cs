namespace QrMenu.Domain.Entities;

public enum RefundStatus
{
    /// <summary>The owner asked for it; waiting for the super admin.</summary>
    Requested,

    /// <summary>The super admin said no.</summary>
    Rejected,

    /// <summary>Razorpay accepted the refund; the money is on its way (usually 5-7 working days).</summary>
    Processing,

    /// <summary>The money went back (Razorpay confirmed it, or the super admin paid it back by hand).</summary>
    Refunded,

    /// <summary>Razorpay could not refund it.</summary>
    Failed
}

/// <summary>
/// Money given back for a plan payment: requested by the owner (within the refund window) or started by the
/// super admin. Online payments are refunded through Razorpay; payments recorded by hand are refunded by hand
/// and only recorded here. A full refund stops the plan at once.
/// </summary>
public class PlanRefund
{
    public Guid Id { get; set; }
    public Guid RestaurantId { get; set; }

    /// <summary>The Razorpay checkout being refunded (online payments).</summary>
    public Guid? PlanPaymentId { get; set; }

    /// <summary>The recorded payment being refunded (payments taken by hand: cash, UPI, bank).</summary>
    public Guid? PaymentEventId { get; set; }

    public string? PlanName { get; set; }

    /// <summary>What the payment was, and what is (to be) given back. Rupees.</summary>
    public decimal PaymentAmount { get; set; }
    public decimal Amount { get; set; }

    /// <summary>Payment charges kept back (Razorpay does not return them to us). 0 when they were given back too, or for manual payments.</summary>
    public decimal Fee { get; set; }

    public RefundStatus Status { get; set; } = RefundStatus.Requested;

    /// <summary>"owner" (asked from My plan) or "admin" (started by the super admin).</summary>
    public string RequestedBy { get; set; } = "owner";
    public string? Reason { get; set; }

    /// <summary>The super admin's note to the owner (why it was rejected, or a reference for a manual refund).</summary>
    public string? AdminNote { get; set; }
    public string? DecidedBy { get; set; }

    /// <summary>Razorpay refund id (rfnd_...).</summary>
    public string? GatewayRefundId { get; set; }

    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DecidedAt { get; set; }
    public DateTime? RefundedAt { get; set; }

    public Restaurant Restaurant { get; set; } = null!;
}

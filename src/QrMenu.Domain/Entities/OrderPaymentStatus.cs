namespace QrMenu.Domain.Entities;

/// <summary>Customer payment for an order: Unpaid → Claimed (customer says they paid by UPI) → Paid (staff confirmed).</summary>
public enum OrderPaymentStatus
{
    Unpaid,
    Claimed,
    Paid
}

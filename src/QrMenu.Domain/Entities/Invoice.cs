namespace QrMenu.Domain.Entities;

/// <summary>
/// A bill for one order or for all unbilled orders at a table. Numbered per restaurant (1, 2, 3 …).
/// Restaurant details and charge rates are copied in, so an invoice never changes after it is issued.
/// The line items come from its orders, which are themselves snapshots.
/// </summary>
public class Invoice
{
    public Guid Id { get; set; }
    public Guid RestaurantId { get; set; }

    /// <summary>Running number within the restaurant.</summary>
    public int Sequence { get; set; }

    /// <summary>What is printed, e.g. "INV-0007".</summary>
    public string Number { get; set; } = string.Empty;

    public string? TableNumber { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }

    public decimal Subtotal { get; set; }
    public decimal ServiceChargeAmount { get; set; }
    public decimal GstAmount { get; set; }
    public decimal Total { get; set; }
    public decimal ServiceChargePercentage { get; set; }
    public decimal GstPercentage { get; set; }

    // Restaurant details at the time of billing.
    public string RestaurantName { get; set; } = string.Empty;
    public string? RestaurantAddress { get; set; }
    public string? RestaurantPhone { get; set; }
    public string? GstNumber { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Restaurant Restaurant { get; set; } = null!;
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}

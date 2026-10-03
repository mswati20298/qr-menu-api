namespace QrMenu.Domain.Entities;

public class Order
{
    public Guid Id { get; set; }
    public Guid RestaurantId { get; set; }
    public Guid? TableId { get; set; }
    public string? TableNumberSnapshot { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string? Note { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Placed;
    public decimal Subtotal { get; set; }
    public decimal ServiceChargeAmount { get; set; }
    public decimal GstAmount { get; set; }
    public decimal Total { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public OrderPaymentStatus PaymentStatus { get; set; } = OrderPaymentStatus.Unpaid;
    /// <summary>UPI transaction id (UTR) the customer typed when claiming, or a note from staff.</summary>
    public string? PaymentReference { get; set; }
    /// <summary>How staff recorded the payment (Cash, Upi, Card …). Set only when Paid.</summary>
    public PaymentMethod? PaymentMethod { get; set; }
    public DateTime? PaymentClaimedAt { get; set; }
    public DateTime? PaidAt { get; set; }

    /// <summary>Set once the order has been billed. An order is on at most one invoice.</summary>
    public Guid? InvoiceId { get; set; }

    public Restaurant Restaurant { get; set; } = null!;
    public Table? Table { get; set; }
    public Invoice? Invoice { get; set; }
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}

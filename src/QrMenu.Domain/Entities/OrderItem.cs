namespace QrMenu.Domain.Entities;

public class OrderItem
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Guid? MenuItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string? VariantName { get; set; }
    public decimal UnitPrice { get; set; }
    public int Qty { get; set; }
    /// <summary>JSON-serialized list of { name, price } for selected add-ons — kept as a snapshot, not a normalized join, since add-ons are immutable once ordered.</summary>
    public string? AddOnsJson { get; set; }
    public decimal LineTotal { get; set; }

    public Order Order { get; set; } = null!;
}

namespace QrMenu.Domain.Entities;

public class Table
{
    public Guid Id { get; set; }
    public Guid RestaurantId { get; set; }
    public string Number { get; set; } = string.Empty;
    public int? Capacity { get; set; }
    public bool IsActive { get; set; } = true;
    /// <summary>Secret printed in this table's QR (?k=). Only the owner can reset it; the QR then needs reprinting.</summary>
    public string QrCode { get; set; } = string.Empty;

    public Restaurant Restaurant { get; set; } = null!;
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}

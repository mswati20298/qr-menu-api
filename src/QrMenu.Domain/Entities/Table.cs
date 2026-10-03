namespace QrMenu.Domain.Entities;

public class Table
{
    public Guid Id { get; set; }
    public Guid RestaurantId { get; set; }
    public string Number { get; set; } = string.Empty;
    public int? Capacity { get; set; }
    public bool IsActive { get; set; } = true;

    public Restaurant Restaurant { get; set; } = null!;
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}

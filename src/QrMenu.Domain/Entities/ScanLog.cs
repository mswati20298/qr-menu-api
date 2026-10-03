namespace QrMenu.Domain.Entities;

public class ScanLog
{
    public Guid Id { get; set; }
    public Guid RestaurantId { get; set; }
    public string? TableNumber { get; set; }
    public DateTime ScannedAt { get; set; } = DateTime.UtcNow;

    public Restaurant Restaurant { get; set; } = null!;
}

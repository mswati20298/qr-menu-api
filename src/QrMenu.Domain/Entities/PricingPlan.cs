namespace QrMenu.Domain.Entities;

/// <summary>
/// A plan in the catalog the super admin maintains (e.g. "Monthly" ₹499 for 1 month).
/// Owners can only buy plans that are active.
/// </summary>
public class PricingPlan
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DurationMonths { get; set; }
    public decimal Price { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

namespace QrMenu.Domain.Entities;

public class RestaurantBackground
{
    public Guid Id { get; set; }
    public Guid RestaurantId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;

    /// <summary>Bit mask of time-of-day slots: Morning=1, Afternoon=2, Evening=4, Night=8.</summary>
    public int Slots { get; set; }

    /// <summary>Exactly one image per restaurant is the default (used in Fixed mode and as a fallback).</summary>
    public bool IsDefault { get; set; }

    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Restaurant Restaurant { get; set; } = null!;
}

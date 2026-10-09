namespace QrMenu.Domain.Entities;

public enum FeedbackKind
{
    /// <summary>A guest rating their order at a restaurant (seen by that restaurant's owner).</summary>
    Customer,

    /// <summary>A restaurant owner rating QRenvo itself (one per restaurant; can become a landing page testimonial).</summary>
    Owner
}

/// <summary>
/// A star rating with an optional comment and photo. Only the super admin can publish one on the landing page.
/// </summary>
public class Feedback
{
    public Guid Id { get; set; }
    public Guid RestaurantId { get; set; }
    public FeedbackKind Kind { get; set; }

    /// <summary>Customer feedback: the order it is about (one feedback per order).</summary>
    public Guid? OrderId { get; set; }

    /// <summary>1 to 5 stars.</summary>
    public int Rating { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Comment { get; set; }

    /// <summary>Photo the person uploaded. Owner feedback without one shows the restaurant logo instead.</summary>
    public string? ImageUrl { get; set; }

    /// <summary>Shown on the QRenvo landing page. Set by the super admin only.</summary>
    public bool IsPublished { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Restaurant Restaurant { get; set; } = null!;
}

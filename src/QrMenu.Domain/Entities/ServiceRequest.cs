namespace QrMenu.Domain.Entities;

/// <summary>A customer at a table asking for help: call the waiter, water, or the bill.</summary>
public class ServiceRequest
{
    public Guid Id { get; set; }
    public Guid RestaurantId { get; set; }

    /// <summary>Table number as it was when the request was made (tables can be renamed or deleted later).</summary>
    public string TableNumber { get; set; } = string.Empty;

    public ServiceRequestType Type { get; set; }
    public ServiceRequestStatus Status { get; set; } = ServiceRequestStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public Restaurant Restaurant { get; set; } = null!;
}

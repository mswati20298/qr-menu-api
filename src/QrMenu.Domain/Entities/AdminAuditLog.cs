namespace QrMenu.Domain.Entities;

/// <summary>
/// One change a super admin made (refund, password reset, plan given, keys saved...). Kept so there is a record of
/// who did what and when. Holds no request body, so no passwords or keys end up here.
/// </summary>
public class AdminAuditLog
{
    public Guid Id { get; set; }
    public string AdminEmail { get; set; } = string.Empty;

    /// <summary>What was done, e.g. "ApproveRefund", "ResetOwnerPassword".</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>The address called, e.g. "POST /api/superadmin/refunds/{id}/approve".</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>The id in the address (restaurant, refund, plan...), when there is one.</summary>
    public string? TargetId { get; set; }

    /// <summary>Our answer: 2xx = done, 4xx = refused (e.g. wrong input), 5xx = failed.</summary>
    public int StatusCode { get; set; }

    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

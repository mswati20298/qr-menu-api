namespace QrMenu.Application.SuperAdmins;

public record AdminAuditEntry(string AdminEmail, string Action, string Path, string? TargetId, int StatusCode, string? IpAddress);

public record AdminAuditLogDto(DateTime CreatedAt, string AdminEmail, string Action, string Path, string? TargetId, int StatusCode, string? IpAddress);

/// <summary>Record of what super admins changed. Writing never throws: a missing log line must not break the action.</summary>
public interface IAdminAuditService
{
    Task WriteAsync(AdminAuditEntry entry, CancellationToken ct = default);

    /// <summary>Newest first.</summary>
    Task<List<AdminAuditLogDto>> ListAsync(int take, CancellationToken ct = default);
}

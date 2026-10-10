using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using QrMenu.Application.SuperAdmins;
using QrMenu.Domain.Entities;
using QrMenu.Infrastructure.Persistence;

namespace QrMenu.Infrastructure.Services;

/// <summary>
/// Writes in its own scope (own DbContext): the request's context may hold half-done changes of an action that
/// failed, and saving the log line must never save those too.
/// </summary>
public class AdminAuditService(IServiceScopeFactory scopes, AppDbContext db, TimeProvider clock, ILogger<AdminAuditService> logger)
    : IAdminAuditService
{
    public async Task WriteAsync(AdminAuditEntry entry, CancellationToken ct = default)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var logDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            logDb.AdminAuditLogs.Add(new AdminAuditLog
            {
                Id = Guid.NewGuid(),
                AdminEmail = Cut(entry.AdminEmail, 256),
                Action = Cut(entry.Action, 100),
                Path = Cut(entry.Path, 300),
                TargetId = entry.TargetId is null ? null : Cut(entry.TargetId, 100),
                StatusCode = entry.StatusCode,
                IpAddress = entry.IpAddress is null ? null : Cut(entry.IpAddress, 64),
                CreatedAt = clock.GetUtcNow().UtcDateTime
            });
            await logDb.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not write the audit log for {Action} by {Admin}", entry.Action, entry.AdminEmail);
        }
    }

    public async Task<List<AdminAuditLogDto>> ListAsync(int take, CancellationToken ct = default) =>
        await db.AdminAuditLogs.AsNoTracking()
            .OrderByDescending(l => l.CreatedAt)
            .Take(Math.Clamp(take, 1, 500))
            .Select(l => new AdminAuditLogDto(l.CreatedAt, l.AdminEmail, l.Action, l.Path, l.TargetId, l.StatusCode, l.IpAddress))
            .ToListAsync(ct);

    private static string Cut(string value, int max) => value.Length <= max ? value : value[..max];
}

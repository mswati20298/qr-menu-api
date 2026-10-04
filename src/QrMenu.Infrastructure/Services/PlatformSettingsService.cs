using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QrMenu.Application.Platform;
using QrMenu.Application.Subscriptions;
using QrMenu.Domain.Entities;
using QrMenu.Infrastructure.Persistence;

namespace QrMenu.Infrastructure.Services;

public class PlatformSettingsService(AppDbContext db, IOptions<SubscriptionSettings> options) : IPlatformSettingsService
{
    public async Task<PlatformSettingsDto> GetAsync(CancellationToken ct = default)
    {
        var settings = await FindAsync(ct);
        return ToDto(settings);
    }

    public async Task<PlatformSettingsDto> UpdateAsync(UpdatePlatformSettingsRequest request, string performedBy, CancellationToken ct = default)
    {
        var settings = await db.PlatformSettings.FirstOrDefaultAsync(s => s.Id == PlatformSettings.SingletonId, ct);
        if (settings is null)
        {
            settings = new PlatformSettings { Id = PlatformSettings.SingletonId };
            db.PlatformSettings.Add(settings);
        }

        settings.TrialDays = request.TrialDays;
        settings.UpdatedAt = DateTime.UtcNow;
        settings.UpdatedBy = performedBy;
        await db.SaveChangesAsync(ct);

        return ToDto(settings);
    }

    public async Task<int> GetTrialDaysAsync(CancellationToken ct = default) => (await FindAsync(ct)).TrialDays;

    /// <summary>The saved row, or (before the super admin has saved anything) the configured default.</summary>
    private async Task<PlatformSettings> FindAsync(CancellationToken ct) =>
        await db.PlatformSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Id == PlatformSettings.SingletonId, ct)
        ?? new PlatformSettings { TrialDays = Math.Max(0, options.Value.TrialDays), UpdatedAt = DateTime.MinValue };

    private PlatformSettingsDto ToDto(PlatformSettings s) =>
        new(s.TrialDays, options.Value.GraceDays, s.UpdatedAt, s.UpdatedBy);
}

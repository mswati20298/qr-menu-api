using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QrMenu.Application.Common;
using QrMenu.Application.Common.Exceptions;
using QrMenu.Application.Common.Interfaces;
using QrMenu.Application.Platform;
using QrMenu.Domain.Entities;
using QrMenu.Infrastructure.Persistence;
using QrMenu.Infrastructure.Persistence.Seed;

namespace QrMenu.Infrastructure.Services;

public class DemoResetService(
    AppDbContext db,
    IOptions<SiteSettings> siteOptions,
    IFileStorageService storage,
    IWebHostEnvironment env,
    TimeProvider clock,
    ILogger<DemoResetService> logger) : IDemoResetService
{
    // Platform data that survives a reset. Everything else belongs to a restaurant and is wiped.
    private static readonly HashSet<Type> Kept = [typeof(PlatformSettings), typeof(SuperAdmin), typeof(PricingPlan)];

    // One reset at a time (button and auto reset could meet).
    private static readonly SemaphoreSlim Gate = new(1, 1);

    private bool IsDemo => siteOptions.Value.IsDemo;

    public async Task<DemoStatusDto> GetStatusAsync(CancellationToken ct = default)
    {
        var settings = await db.PlatformSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Id == PlatformSettings.SingletonId, ct);
        return ToDto(settings);
    }

    public async Task<DemoStatusDto> UpdateSettingsAsync(UpdateDemoSettingsRequest request, string performedBy, CancellationToken ct = default)
    {
        EnsureDemo();
        var settings = await GetOrCreateAsync(ct);
        settings.DemoAutoResetDays = request.AutoResetDays;
        settings.LastDemoResetAt ??= clock.GetUtcNow().UtcDateTime;
        settings.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        settings.UpdatedBy = performedBy;
        await db.SaveChangesAsync(ct);
        return ToDto(settings);
    }

    public async Task<DemoStatusDto> ResetAsync(string performedBy, CancellationToken ct = default)
    {
        EnsureDemo();
        await Gate.WaitAsync(ct);
        try
        {
            await using (var transaction = await db.Database.BeginTransactionAsync(ct))
            {
                foreach (var table in TablesToWipe())
                {
#pragma warning disable EF1002 // Table names come from the EF model, not from input.
                    await db.Database.ExecuteSqlRawAsync($"DELETE FROM {table}", ct);
#pragma warning restore EF1002
                }
                await transaction.CommitAsync(ct);
            }

            DeleteUploadedFiles();

            db.ChangeTracker.Clear();
            await DbSeeder.SeedAsync(db, demoData: true, storage);

            var settings = await GetOrCreateAsync(ct);
            settings.LastDemoResetAt = clock.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(ct);

            logger.LogInformation("Demo data reset by {By}", performedBy);
            return ToDto(settings);
        }
        finally
        {
            Gate.Release();
        }
    }

    public async Task<bool> ResetIfDueAsync(CancellationToken ct = default)
    {
        if (!IsDemo)
        {
            return false;
        }

        var settings = await GetOrCreateAsync(ct);
        var now = clock.GetUtcNow().UtcDateTime;

        if (settings.LastDemoResetAt is null)
        {
            // First run with this feature: start counting from now instead of wiping straight away.
            settings.LastDemoResetAt = now;
            await db.SaveChangesAsync(ct);
            return false;
        }

        if (settings.DemoAutoResetDays <= 0 || now < settings.LastDemoResetAt.Value.AddDays(settings.DemoAutoResetDays))
        {
            return false;
        }

        await ResetAsync("auto reset", ct);
        return true;
    }

    /// <summary>Restaurant tables, each one after every table that points to it (so foreign keys never block a delete).</summary>
    private List<string> TablesToWipe()
    {
        var types = db.Model.GetEntityTypes()
            .Where(t => !t.IsOwned() && t.GetTableName() is not null && !Kept.Contains(t.ClrType))
            .ToList();
        var inScope = types.ToHashSet();
        var visited = new HashSet<IEntityType>();
        var ordered = new List<IEntityType>();

        void Visit(IEntityType type)
        {
            if (!visited.Add(type))
            {
                return;
            }
            foreach (var fk in type.GetReferencingForeignKeys())
            {
                if (inScope.Contains(fk.DeclaringEntityType))
                {
                    Visit(fk.DeclaringEntityType);
                }
            }
            ordered.Add(type);
        }

        types.ForEach(Visit);

        return ordered
            .Select(t => t.GetSchema() is { } schema ? $"[{schema}].[{t.GetTableName()}]" : $"[{t.GetTableName()}]")
            .Distinct()
            .ToList();
    }

    private void DeleteUploadedFiles()
    {
        var folder = Path.Combine(env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"), "uploads");
        if (!Directory.Exists(folder))
        {
            return;
        }
        foreach (var file in Directory.EnumerateFiles(folder))
        {
            try
            {
                File.Delete(file);
            }
            catch (IOException ex)
            {
                logger.LogWarning(ex, "Could not delete {File} during the demo reset", file);
            }
        }
    }

    private void EnsureDemo()
    {
        if (!IsDemo)
        {
            throw new ForbiddenException("This is only available on the demo.");
        }
    }

    private async Task<PlatformSettings> GetOrCreateAsync(CancellationToken ct)
    {
        var settings = await db.PlatformSettings.FirstOrDefaultAsync(s => s.Id == PlatformSettings.SingletonId, ct);
        if (settings is null)
        {
            settings = new PlatformSettings { Id = PlatformSettings.SingletonId };
            db.PlatformSettings.Add(settings);
        }
        return settings;
    }

    private DemoStatusDto ToDto(PlatformSettings? s)
    {
        var days = s?.DemoAutoResetDays ?? new PlatformSettings().DemoAutoResetDays;
        var last = s?.LastDemoResetAt;
        DateTime? next = days > 0 && last is not null ? last.Value.AddDays(days) : null;
        return new DemoStatusDto(IsDemo, days, last, next);
    }
}

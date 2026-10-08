using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QrMenu.Application.Common;
using QrMenu.Application.Platform;

namespace QrMenu.Infrastructure.Services;

/// <summary>Demo deployment only: every half hour, resets the sample data once the auto-reset period has passed.</summary>
public class DemoAutoResetWorker(IServiceScopeFactory scopes, IOptions<SiteSettings> siteOptions, ILogger<DemoAutoResetWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!siteOptions.Value.IsDemo)
        {
            return;
        }

        try
        {
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(30));
            do
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    if (await scope.ServiceProvider.GetRequiredService<IDemoResetService>().ResetIfDueAsync(stoppingToken))
                    {
                        logger.LogInformation("Demo data was reset automatically");
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Automatic demo reset failed");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
    }
}

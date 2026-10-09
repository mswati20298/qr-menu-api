using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using QrMenu.Domain.Entities;
using QrMenu.Infrastructure.Persistence;

namespace QrMenu.Infrastructure.Payments;

/// <summary>
/// Saves gateway messages for the payment log. Uses its own database context and saves straight away, so an entry
/// survives even when the payment step that wrote it fails, and it never mixes with that step's unsaved changes.
/// Writing a log entry never throws.
/// </summary>
public class PaymentGatewayLogWriter(IServiceScopeFactory scopes, TimeProvider clock, ILogger<PaymentGatewayLogWriter> logger)
{
    // Webhooks are small; this is only a guard against a giant body filling the table.
    private const int MaxBodyLength = 20_000;

    public async Task WriteAsync(string kind, string? orderId, int? statusCode, string? request, string? response, string? note)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.PaymentGatewayLogs.Add(new PaymentGatewayLog
            {
                Id = Guid.NewGuid(),
                Kind = kind,
                GatewayOrderId = Trim(orderId, 64),
                StatusCode = statusCode,
                RequestBody = Trim(request, MaxBodyLength),
                ResponseBody = Trim(response, MaxBodyLength),
                Note = Trim(note, 500),
                CreatedAt = clock.GetUtcNow().UtcDateTime
            });
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not save a {Kind} payment log entry for {OrderId}", kind, orderId);
        }
    }

    private static string? Trim(string? value, int max) =>
        string.IsNullOrEmpty(value) ? value : value.Length <= max ? value : value[..max] + "…(cut)";
}

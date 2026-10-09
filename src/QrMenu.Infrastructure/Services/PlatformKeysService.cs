using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QrMenu.Application.Common;
using QrMenu.Application.Common.Interfaces;
using QrMenu.Application.Platform;
using QrMenu.Domain.Entities;
using QrMenu.Infrastructure.Persistence;
using QrMenu.Infrastructure.Security;

namespace QrMenu.Infrastructure.Services;

public class PlatformKeysService(
    AppDbContext db,
    SecretProtector protector,
    IntegrationKeyStore store,
    IPaymentGateway gateway,
    IOptions<SiteSettings> siteOptions) : IPlatformKeysService
{
    public async Task<PlatformKeysDto> GetAsync(CancellationToken ct = default)
    {
        var settings = await db.PlatformSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Id == PlatformSettings.SingletonId, ct);
        return ToDto(settings);
    }

    public async Task<PlatformKeysDto> UpdateAsync(UpdatePlatformKeysRequest request, string performedBy, CancellationToken ct = default)
    {
        var settings = await db.PlatformSettings.FirstOrDefaultAsync(s => s.Id == PlatformSettings.SingletonId, ct);
        if (settings is null)
        {
            settings = new PlatformSettings { Id = PlatformSettings.SingletonId };
            db.PlatformSettings.Add(settings);
        }

        if (request.RazorpayKeyId is not null)
        {
            settings.RazorpayKeyId = Clean(request.RazorpayKeyId);
        }
        if (request.RazorpayKeySecret is not null)
        {
            settings.RazorpayKeySecretEncrypted = Encrypt(request.RazorpayKeySecret);
        }
        if (request.RazorpayWebhookSecret is not null)
        {
            settings.RazorpayWebhookSecretEncrypted = Encrypt(request.RazorpayWebhookSecret);
        }
        if (request.GeminiApiKey is not null)
        {
            settings.GeminiApiKeyEncrypted = Encrypt(request.GeminiApiKey);
        }

        settings.UpdatedAt = DateTime.UtcNow;
        settings.UpdatedBy = performedBy;
        await db.SaveChangesAsync(ct);

        store.SetPanelKeys(Decrypt(settings));
        return ToDto(settings);
    }

    public async Task<KeyCheckResultDto> TestRazorpayAsync(TestRazorpayKeysRequest request, CancellationToken ct = default)
    {
        var keyId = string.IsNullOrWhiteSpace(request.KeyId) ? store.RazorpayKeyId : request.KeyId.Trim();
        var keySecret = string.IsNullOrWhiteSpace(request.KeySecret) ? store.RazorpayKeySecret : request.KeySecret.Trim();
        var result = await gateway.CheckCredentialsAsync(keyId, keySecret, ct);
        return new KeyCheckResultDto(result.Ok, result.Message);
    }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        var settings = await db.PlatformSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Id == PlatformSettings.SingletonId, ct);
        store.SetPanelKeys(settings is null ? PanelKeys.None : Decrypt(settings));
    }

    private PanelKeys Decrypt(PlatformSettings s) => new(
        Clean(s.RazorpayKeyId),
        protector.Unprotect(s.RazorpayKeySecretEncrypted),
        protector.Unprotect(s.RazorpayWebhookSecretEncrypted),
        protector.Unprotect(s.GeminiApiKeyEncrypted));

    private string? Encrypt(string value) => Clean(value) is { } v ? protector.Protect(v) : null;

    private static string? Clean(string? value) => value.CleanOrNull();

    private PlatformKeysDto ToDto(PlatformSettings? settings)
    {
        var panel = settings is null ? PanelKeys.None : Decrypt(settings);
        var keyId = panel.RazorpayKeyId ?? Clean(store.ServerRazorpayKeyId);

        var mode = keyId switch
        {
            null => "none",
            _ when keyId.StartsWith("rzp_live_", StringComparison.Ordinal) => "live",
            _ => "test"
        };

        return new PlatformKeysDto(
            keyId,
            Source(panel.RazorpayKeyId, store.ServerRazorpayKeyId),
            mode,
            Status(panel.RazorpayKeySecret, store.ServerRazorpayKeySecret),
            Status(panel.RazorpayWebhookSecret, store.ServerRazorpayWebhookSecret),
            Status(panel.GeminiApiKey, store.ServerGeminiApiKey),
            $"{siteOptions.Value.AppUrl.TrimEnd('/')}/api/payments/razorpay/webhook",
            settings?.UpdatedAt ?? DateTime.MinValue,
            settings?.UpdatedBy);
    }

    private static string Source(string? panel, string? server) =>
        !string.IsNullOrWhiteSpace(panel) ? "panel" : !string.IsNullOrWhiteSpace(server) ? "server" : "none";

    private static SecretKeyStatus Status(string? panel, string? server)
    {
        var value = !string.IsNullOrWhiteSpace(panel) ? panel : server;
        var source = Source(panel, server);
        return string.IsNullOrWhiteSpace(value)
            ? new SecretKeyStatus(false, source, null)
            : new SecretKeyStatus(true, source, value.Length > 8 ? value[^4..] : null);
    }
}

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using QrMenu.Infrastructure.Payments;

namespace QrMenu.Infrastructure.Security;

/// <summary>Keys saved in the super admin panel (already decrypted). Null = not set there.</summary>
public sealed record PanelKeys(string? RazorpayKeyId, string? RazorpayKeySecret, string? RazorpayWebhookSecret, string? GeminiApiKey)
{
    public static readonly PanelKeys None = new(null, null, null, null);
}

/// <summary>
/// The payment and AI keys in use right now: a key saved in the super admin panel wins, otherwise the one from
/// the server configuration (.env). Kept in memory; loaded at startup and replaced whenever the panel saves.
/// </summary>
public class IntegrationKeyStore(IOptions<RazorpaySettings> razorpayOptions, IConfiguration configuration)
{
    private volatile PanelKeys _panel = PanelKeys.None;

    public PanelKeys Panel => _panel;

    public void SetPanelKeys(PanelKeys keys) => _panel = keys;

    public string RazorpayKeyId => Pick(_panel.RazorpayKeyId, razorpayOptions.Value.KeyId);
    public string RazorpayKeySecret => Pick(_panel.RazorpayKeySecret, razorpayOptions.Value.KeySecret);
    public string RazorpayWebhookSecret => Pick(_panel.RazorpayWebhookSecret, razorpayOptions.Value.WebhookSecret);
    public string GeminiApiKey => Pick(_panel.GeminiApiKey, configuration["Gemini:ApiKey"]);

    public string ServerRazorpayKeyId => razorpayOptions.Value.KeyId ?? string.Empty;
    public string ServerRazorpayKeySecret => razorpayOptions.Value.KeySecret ?? string.Empty;
    public string ServerRazorpayWebhookSecret => razorpayOptions.Value.WebhookSecret ?? string.Empty;
    public string ServerGeminiApiKey => configuration["Gemini:ApiKey"] ?? string.Empty;

    private static string Pick(string? panel, string? server) =>
        !string.IsNullOrWhiteSpace(panel) ? panel : server ?? string.Empty;
}

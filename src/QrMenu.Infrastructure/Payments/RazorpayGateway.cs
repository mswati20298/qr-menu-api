using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using QrMenu.Application.Common.Exceptions;
using QrMenu.Application.Common.Interfaces;
using QrMenu.Infrastructure.Security;

namespace QrMenu.Infrastructure.Payments;

/// <summary>Keys come from <see cref="IntegrationKeyStore"/>: the super admin panel first, then the server configuration.</summary>
public class RazorpayGateway(HttpClient http, IntegrationKeyStore keys, ILogger<RazorpayGateway> logger) : IPaymentGateway
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(keys.RazorpayKeyId) && !string.IsNullOrWhiteSpace(keys.RazorpayKeySecret);

    public string KeyId => keys.RazorpayKeyId;

    public async Task<string> CreateOrderAsync(
        long amountInPaise, string currency, string receipt, IDictionary<string, string> notes, CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            throw new ConflictException("Online payments are not set up yet. Please contact support.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.razorpay.com/v1/orders")
        {
            Content = JsonContent.Create(new { amount = amountInPaise, currency, receipt, notes })
        };
        request.Headers.Authorization = BasicAuth(keys.RazorpayKeyId, keys.RazorpayKeySecret);

        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            logger.LogError("Razorpay order creation failed: {Status} {Body}", (int)response.StatusCode, body);
            throw new ConflictException("Could not start the payment. Please try again in a moment.");
        }

        var order = await response.Content.ReadFromJsonAsync<RazorpayOrder>(cancellationToken: ct);
        return order?.Id ?? throw new ConflictException("Could not start the payment. Please try again in a moment.");
    }

    // Razorpay signs "order_id|payment_id" with the key secret.
    public bool IsPaymentSignatureValid(string orderId, string paymentId, string signature) =>
        IsConfigured && SignatureMatches($"{orderId}|{paymentId}", keys.RazorpayKeySecret, signature);

    // The webhook signs the raw request body with the webhook secret.
    public bool IsWebhookSignatureValid(string body, string? signature) =>
        !string.IsNullOrWhiteSpace(keys.RazorpayWebhookSecret)
        && !string.IsNullOrWhiteSpace(signature)
        && SignatureMatches(body, keys.RazorpayWebhookSecret, signature);

    public async Task<GatewayCheckResult> CheckCredentialsAsync(string keyId, string keySecret, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(keyId) || string.IsNullOrWhiteSpace(keySecret))
        {
            return new GatewayCheckResult(false, "Add both the Key ID and the Key Secret first.");
        }

        // Cheapest authenticated call: list at most one order.
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.razorpay.com/v1/orders?count=1");
        request.Headers.Authorization = BasicAuth(keyId, keySecret);

        try
        {
            using var response = await http.SendAsync(request, ct);
            if (response.IsSuccessStatusCode)
            {
                var mode = keyId.StartsWith("rzp_live_", StringComparison.Ordinal) ? "live" : "test";
                return new GatewayCheckResult(true, $"Connected to Razorpay ({mode} mode).");
            }

            return (int)response.StatusCode == 401
                ? new GatewayCheckResult(false, "Razorpay refused these keys. Check the Key ID and Key Secret.")
                : new GatewayCheckResult(false, $"Razorpay answered {(int)response.StatusCode}. Try again in a moment.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Razorpay key check could not reach Razorpay");
            return new GatewayCheckResult(false, "Could not reach Razorpay. Try again in a moment.");
        }
    }

    private static AuthenticationHeaderValue BasicAuth(string keyId, string keySecret) =>
        new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{keyId}:{keySecret}")));

    private static bool SignatureMatches(string payload, string secret, string signature)
    {
        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(payload));
        var expectedHex = Encoding.ASCII.GetBytes(Convert.ToHexString(expected).ToLowerInvariant());
        var actual = Encoding.ASCII.GetBytes(signature.Trim().ToLowerInvariant());
        return CryptographicOperations.FixedTimeEquals(expectedHex, actual);
    }

    private sealed record RazorpayOrder([property: JsonPropertyName("id")] string Id);
}

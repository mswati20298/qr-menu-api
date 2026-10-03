using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QrMenu.Application.Common.Exceptions;
using QrMenu.Application.Common.Interfaces;

namespace QrMenu.Infrastructure.Payments;

public class RazorpayGateway(HttpClient http, IOptions<RazorpaySettings> options, ILogger<RazorpayGateway> logger) : IPaymentGateway
{
    private readonly RazorpaySettings _settings = options.Value;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_settings.KeyId) && !string.IsNullOrWhiteSpace(_settings.KeySecret);

    public string KeyId => _settings.KeyId;

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
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_settings.KeyId}:{_settings.KeySecret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

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
        IsConfigured && SignatureMatches($"{orderId}|{paymentId}", _settings.KeySecret, signature);

    // The webhook signs the raw request body with the webhook secret.
    public bool IsWebhookSignatureValid(string body, string? signature) =>
        !string.IsNullOrWhiteSpace(_settings.WebhookSecret)
        && !string.IsNullOrWhiteSpace(signature)
        && SignatureMatches(body, _settings.WebhookSecret, signature);

    private static bool SignatureMatches(string payload, string secret, string signature)
    {
        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(payload));
        var expectedHex = Encoding.ASCII.GetBytes(Convert.ToHexString(expected).ToLowerInvariant());
        var actual = Encoding.ASCII.GetBytes(signature.Trim().ToLowerInvariant());
        return CryptographicOperations.FixedTimeEquals(expectedHex, actual);
    }

    private sealed record RazorpayOrder([property: JsonPropertyName("id")] string Id);
}

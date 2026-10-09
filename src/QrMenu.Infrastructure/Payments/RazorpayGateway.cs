using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using QrMenu.Application.Common.Exceptions;
using QrMenu.Application.Common.Interfaces;
using QrMenu.Infrastructure.Security;

namespace QrMenu.Infrastructure.Payments;

/// <summary>Keys come from <see cref="IntegrationKeyStore"/>: the super admin panel first, then the server configuration.</summary>
public class RazorpayGateway(HttpClient http, IntegrationKeyStore keys, ILogger<RazorpayGateway> logger, PaymentGatewayLogWriter? paymentLog = null)
    : IPaymentGateway
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

        var payload = new { amount = amountInPaise, currency, receipt, notes };
        var requestJson = JsonSerializer.Serialize(payload);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.razorpay.com/v1/orders")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Authorization = BasicAuth(keys.RazorpayKeyId, keys.RazorpayKeySecret);

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            await Log("order.create", null, null, requestJson, null, $"No answer from Razorpay: {ex.Message}");
            throw new ConflictException("Could not start the payment. Please try again in a moment.");
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Razorpay order creation failed: {Status} {Body}", (int)response.StatusCode, body);
                await Log("order.create", null, (int)response.StatusCode, requestJson, body, "Razorpay refused the order");
                throw new ConflictException("Could not start the payment. Please try again in a moment.");
            }

            var order = JsonSerializer.Deserialize<RazorpayOrder>(body);
            await Log("order.create", order?.Id, (int)response.StatusCode, requestJson, body, "Order created");
            return order?.Id ?? throw new ConflictException("Could not start the payment. Please try again in a moment.");
        }
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

    public async Task<GatewayRefundResult> RefundAsync(string paymentId, long amountInPaise, string receipt, string? orderId, CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            throw new ConflictException("Online payments are not set up, so Razorpay cannot refund. Add the keys first.");
        }

        // "normal" speed: free, 5-7 working days. receipt ties the refund to our record in Razorpay's dashboard.
        var payload = new { amount = amountInPaise, speed = "normal", receipt, notes = new Dictionary<string, string> { ["source"] = "QRenvo" } };
        var requestJson = JsonSerializer.Serialize(payload);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://api.razorpay.com/v1/payments/{Uri.EscapeDataString(paymentId)}/refund")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Authorization = BasicAuth(keys.RazorpayKeyId, keys.RazorpayKeySecret);

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            await Log("refund.create", orderId, null, requestJson, null, $"No answer from Razorpay: {ex.Message}");
            throw new ConflictException("Razorpay did not answer. Nothing was refunded; please try again.");
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Razorpay refund failed: {Status} {Body}", (int)response.StatusCode, body);
                await Log("refund.create", orderId, (int)response.StatusCode, requestJson, body, "Razorpay refused the refund");
                throw new ConflictException(response.StatusCode == System.Net.HttpStatusCode.NotFound
                    ? "Razorpay does not know this payment. Is it from the other mode (test vs live keys)? Nothing was refunded."
                    : $"Razorpay refused the refund: {ReadError(body) ?? "unknown reason"}");
            }

            var refund = JsonSerializer.Deserialize<RazorpayRefund>(body)
                ?? throw new ConflictException("Razorpay's answer could not be read.");
            await Log("refund.create", orderId, (int)response.StatusCode, requestJson, body, $"Refund {refund.Id} created ({refund.Status})");
            return new GatewayRefundResult(refund.Id, refund.Status ?? "pending");
        }
    }

    public async Task<long?> GetPaymentFeeAsync(string paymentId, CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            return null;
        }
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.razorpay.com/v1/payments/{Uri.EscapeDataString(paymentId)}");
            request.Headers.Authorization = BasicAuth(keys.RazorpayKeyId, keys.RazorpayKeySecret);
            using var response = await http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Razorpay did not return payment {PaymentId}: {Status}", paymentId, (int)response.StatusCode);
                return null;
            }
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            // "fee" already includes GST ("tax" is the GST part of it).
            return doc.RootElement.TryGetProperty("fee", out var fee) && fee.ValueKind == JsonValueKind.Number ? fee.GetInt64() : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(ex, "Could not read the fee of Razorpay payment {PaymentId}", paymentId);
            return null;
        }
    }

    /// <summary>Razorpay errors look like {"error":{"description":"..."}}; its gateway answers {"message":"..."}.</summary>
    private static string? ReadError(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.TryGetProperty("error", out var e) && e.TryGetProperty("description", out var d))
            {
                return d.GetString();
            }
            return root.TryGetProperty("message", out var m) ? m.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private Task Log(string kind, string? orderId, int? status, string? request, string? response, string note) =>
        paymentLog?.WriteAsync(kind, orderId, status, request, response, note) ?? Task.CompletedTask;

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

    private sealed record RazorpayRefund([property: JsonPropertyName("id")] string Id, [property: JsonPropertyName("status")] string? Status);
}

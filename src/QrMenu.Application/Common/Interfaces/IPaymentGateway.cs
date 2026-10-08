namespace QrMenu.Application.Common.Interfaces;

public record GatewayCheckResult(bool Ok, string Message);

/// <summary>Online payments (Razorpay). Amounts are in the smallest unit (paise).</summary>
public interface IPaymentGateway
{
    /// <summary>False until the key id and secret are set (super admin panel or server configuration).</summary>
    bool IsConfigured { get; }
    string KeyId { get; }

    Task<string> CreateOrderAsync(long amountInPaise, string currency, string receipt, IDictionary<string, string> notes, CancellationToken ct = default);
    bool IsPaymentSignatureValid(string orderId, string paymentId, string signature);
    bool IsWebhookSignatureValid(string body, string? signature);

    /// <summary>Asks Razorpay whether this key pair works, without charging anything.</summary>
    Task<GatewayCheckResult> CheckCredentialsAsync(string keyId, string keySecret, CancellationToken ct = default);
}

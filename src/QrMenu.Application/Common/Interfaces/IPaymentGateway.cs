namespace QrMenu.Application.Common.Interfaces;

public record GatewayCheckResult(bool Ok, string Message);

/// <summary>Status: Razorpay's refund status ("processed" or "pending").</summary>
public record GatewayRefundResult(string RefundId, string Status);

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

    /// <summary>Gives money back on a captured payment. orderId only links the log entries.</summary>
    /// <exception cref="Exceptions.ConflictException">Razorpay refused or did not answer; the message says why.</exception>
    Task<GatewayRefundResult> RefundAsync(string paymentId, long amountInPaise, string receipt, string? orderId, CancellationToken ct = default);

    /// <summary>Razorpay's charges on a payment (incl. GST, paise), or null when it cannot be read. Never throws.</summary>
    Task<long?> GetPaymentFeeAsync(string paymentId, CancellationToken ct = default);
}

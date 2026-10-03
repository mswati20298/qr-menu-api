namespace QrMenu.Application.Common.Interfaces;

/// <summary>Online payments (Razorpay). Amounts are in the smallest unit (paise).</summary>
public interface IPaymentGateway
{
    /// <summary>False until the key id and secret are set in configuration.</summary>
    bool IsConfigured { get; }
    string KeyId { get; }

    Task<string> CreateOrderAsync(long amountInPaise, string currency, string receipt, IDictionary<string, string> notes, CancellationToken ct = default);
    bool IsPaymentSignatureValid(string orderId, string paymentId, string signature);
    bool IsWebhookSignatureValid(string body, string? signature);
}

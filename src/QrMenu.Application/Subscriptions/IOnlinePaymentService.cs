namespace QrMenu.Application.Subscriptions;

/// <summary>The owner buys a plan online. The plan is applied only after the gateway's signature checks out.</summary>
public interface IOnlinePaymentService
{
    Task<CheckoutDto> StartCheckoutAsync(Guid restaurantId, Guid userId, StartCheckoutRequest request, CancellationToken ct = default);
    Task<OwnerPlanDto> ConfirmCheckoutAsync(Guid restaurantId, ConfirmCheckoutRequest request, CancellationToken ct = default);

    /// <summary>Razorpay webhook (payment.captured / order.paid). Returns false when the signature is wrong.</summary>
    Task<bool> HandleWebhookAsync(string body, string? signature, CancellationToken ct = default);
}

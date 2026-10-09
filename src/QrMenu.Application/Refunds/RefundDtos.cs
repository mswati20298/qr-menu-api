using FluentValidation;

namespace QrMenu.Application.Refunds;

public static class RefundRules
{
    /// <summary>Owners can ask for a refund within this many days of paying (matches the published refund policy).</summary>
    public const int RequestWindowDays = 7;

    /// <summary>Used only when Razorpay cannot tell us the real charges: 2% + 18% GST.</summary>
    public const decimal EstimatedFeePercent = 2.36m;

    public static decimal EstimateFee(decimal amount) => Math.Round(amount * EstimatedFeePercent / 100, 2);
}

/// <summary>A refund as both panels show it. Amounts in rupees. Source: "online" (Razorpay) or "manual".</summary>
public record RefundDto(
    Guid Id,
    Guid RestaurantId,
    string RestaurantName,
    string Source,
    string? PlanName,
    decimal PaymentAmount,
    decimal Amount,
    string Status,
    string RequestedBy,
    string? Reason,
    string? AdminNote,
    string? DecidedBy,
    string? GatewayRefundId,
    string? GatewayPaymentId,
    DateTime RequestedAt,
    DateTime? DecidedAt,
    DateTime? RefundedAt,
    decimal Fee);

/// <summary>A paid plan the owner may still ask a refund for (within the window, nothing asked yet).</summary>
/// <summary>Fee = payment charges Razorpay keeps; RefundAmount = what the owner would get back (Amount - Fee).</summary>
public record RefundablePaymentDto(Guid PlanPaymentId, string PlanName, decimal Amount, decimal Fee, decimal RefundAmount, DateTime PaidAt, DateTime RefundableUntil);

/// <summary>What My plan shows about refunds.</summary>
public record OwnerRefundsDto(List<RefundablePaymentDto> Refundable, List<RefundDto> Refunds);

public record RequestRefundRequest(Guid PlanPaymentId, string Reason);

/// <summary>
/// Amount null = everything not refunded yet, less Razorpay's charges. IncludeFee = give the charges back too
/// (charged twice, or our mistake); then we bear them.
/// </summary>
public record ApproveRefundRequest(decimal? Amount, string? Note, bool IncludeFee = false);

public record RejectRefundRequest(string Note);

/// <summary>
/// Super admin refunds a payment without a request. Exactly one of PlanPaymentId (online: refunded through Razorpay)
/// or PaymentEventId (recorded by hand: you give the money back yourself, this only records it).
/// </summary>
public record AdminRefundRequest(Guid? PlanPaymentId, Guid? PaymentEventId, decimal? Amount, string? Note, bool IncludeFee = false);

public class RequestRefundRequestValidator : AbstractValidator<RequestRefundRequest>
{
    public RequestRefundRequestValidator()
    {
        RuleFor(x => x.PlanPaymentId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Please tell us why you want a refund.").MaximumLength(500);
    }
}

public class ApproveRefundRequestValidator : AbstractValidator<ApproveRefundRequest>
{
    public ApproveRefundRequestValidator()
    {
        RuleFor(x => x.Amount).GreaterThanOrEqualTo(1).When(x => x.Amount is not null).WithMessage("The smallest refund is ₹1.");
        RuleFor(x => x.Note).MaximumLength(500);
    }
}

public class RejectRefundRequestValidator : AbstractValidator<RejectRefundRequest>
{
    public RejectRefundRequestValidator()
    {
        RuleFor(x => x.Note).NotEmpty().WithMessage("Please tell the owner why.").MaximumLength(500);
    }
}

public class AdminRefundRequestValidator : AbstractValidator<AdminRefundRequest>
{
    public AdminRefundRequestValidator()
    {
        RuleFor(x => x).Must(x => (x.PlanPaymentId is null) != (x.PaymentEventId is null))
            .WithMessage("Choose one payment to refund.");
        RuleFor(x => x.Amount).GreaterThanOrEqualTo(1).When(x => x.Amount is not null).WithMessage("The smallest refund is ₹1.");
        RuleFor(x => x.Note).MaximumLength(500);
    }
}

public interface IRefundService
{
    // Owner.
    Task<OwnerRefundsDto> GetOwnerRefundsAsync(Guid restaurantId, CancellationToken ct = default);
    Task<RefundDto> RequestAsync(Guid restaurantId, RequestRefundRequest request, CancellationToken ct = default);

    // Super admin. status: "requested", "processing", "refunded", "rejected", "failed" or empty for all.
    Task<List<RefundDto>> ListAsync(string? status, CancellationToken ct = default);
    Task<RefundDto> ApproveAsync(Guid refundId, ApproveRefundRequest request, string performedBy, CancellationToken ct = default);
    Task<RefundDto> RejectAsync(Guid refundId, RejectRefundRequest request, string performedBy, CancellationToken ct = default);
    Task<RefundDto> RefundDirectAsync(AdminRefundRequest request, string performedBy, CancellationToken ct = default);

    /// <summary>Razorpay webhook refund.processed / refund.failed.</summary>
    Task HandleGatewayUpdateAsync(string gatewayRefundId, string gatewayStatus, CancellationToken ct = default);
}

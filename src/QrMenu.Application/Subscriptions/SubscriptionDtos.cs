namespace QrMenu.Application.Subscriptions;

/// <summary>
/// Plan: "Trial", "Free" or "Paid"; PlanName is what people see (e.g. "Yearly").
/// Status: "Active", "Grace", "Expired" or "Cancelled".
/// ExpiresAt null = no end date. DaysLeft = days until ordering stops (null when it never does).
/// </summary>
public record SubscriptionSummaryDto(
    string Plan,
    string PlanName,
    string Status,
    DateTime? ExpiresAt,
    DateTime? GraceEndsAt,
    DateTime? CancelledAt,
    int? DaysLeft,
    bool CanTakeOrders,
    int GraceDays);

/// <summary>Action: "TrialStarted", "FreeGranted", "PaymentRecorded", "Extended" or "Cancelled".</summary>
public record SubscriptionEventDto(
    Guid Id,
    string Action,
    string Plan,
    string PlanName,
    DateTime? ExpiresAt,
    decimal? Amount,
    string? PaymentMethod,
    string? PaymentReference,
    string? Note,
    string PerformedBy,
    DateTime CreatedAt);

public record SubscriptionDetailsDto(
    Guid RestaurantId,
    string RestaurantName,
    SubscriptionSummaryDto Current,
    List<SubscriptionEventDto> History);

/// <summary>What the owner sees: no internal notes and no super admin emails.</summary>
public record OwnerPlanEventDto(
    string Action,
    string PlanName,
    DateTime? ExpiresAt,
    decimal? Amount,
    string? PaymentMethod,
    string? PaymentReference,
    DateTime CreatedAt);

/// <summary>AvailablePlans = active catalog plans the owner can buy online.</summary>
public record OwnerPlanDto(
    SubscriptionSummaryDto Current,
    List<OwnerPlanEventDto> History,
    List<PricingPlanDto> AvailablePlans,
    bool OnlinePaymentsEnabled);

/// <summary>Lifetime = no end date; otherwise Until (UTC) is required.</summary>
public record GrantFreePlanRequest(bool Lifetime, DateTime? Until, string? Note);

/// <summary>
/// Offline payment recorded by the super admin. PaymentMethod: "Cash", "Upi", "BankTransfer", "Card" or "Other".
/// Periods = how many times the plan's duration. A renewal of a running paid plan continues from its current
/// end date, so no paid days are lost.
/// </summary>
public record RecordPaymentRequest(
    Guid PricingPlanId,
    int Periods,
    decimal Amount,
    string PaymentMethod,
    string? PaymentReference,
    string? Note);

/// <summary>Moves the end date of the current plan to Until (UTC), without a payment.</summary>
public record ExtendPlanRequest(DateTime Until, string? Note);

public record CancelPlanRequest(string? Note);

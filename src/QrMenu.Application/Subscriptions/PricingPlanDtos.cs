namespace QrMenu.Application.Subscriptions;

public record PricingPlanDto(
    Guid Id,
    string Name,
    string? Description,
    int DurationMonths,
    decimal Price,
    bool IsActive,
    int SortOrder);

/// <summary>Super admin view: also how many times the plan was bought (a bought plan cannot be deleted).</summary>
public record PricingPlanAdminDto(
    Guid Id,
    string Name,
    string? Description,
    int DurationMonths,
    decimal Price,
    bool IsActive,
    int SortOrder,
    int TimesPurchased,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record SavePricingPlanRequest(
    string Name,
    string? Description,
    int DurationMonths,
    decimal Price,
    bool IsActive,
    int SortOrder);

/// <summary>The owner chose a plan to buy online.</summary>
public record StartCheckoutRequest(Guid PricingPlanId);

/// <summary>Everything the browser needs to open the Razorpay checkout. Amount is in paise.</summary>
public record CheckoutDto(
    string KeyId,
    string OrderId,
    long Amount,
    string Currency,
    string PlanName,
    string RestaurantName,
    string? OwnerName,
    string? OwnerEmail,
    string? Contact);

/// <summary>What Razorpay's checkout hands back after a successful payment.</summary>
public record ConfirmCheckoutRequest(string RazorpayOrderId, string RazorpayPaymentId, string RazorpaySignature);

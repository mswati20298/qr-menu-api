using FluentValidation;

namespace QrMenu.Application.SuperAdmins;

public record SuperAdminLoginRequest(string Email, string Password);

/// <summary>
/// With two-step login on, the password step returns RequiresTwoFactor + ChallengeToken (no Token); the app then sends
/// the 6-digit code with the challenge to verify-2fa and gets the real token.
/// </summary>
public record SuperAdminAuthResponse(string Token, string Name, bool RequiresTwoFactor = false, string? ChallengeToken = null);

/// <summary>Code: the 6 digits from the app, or one of the recovery codes.</summary>
public record VerifyTwoFactorRequest(string ChallengeToken, string Code);

/// <summary>The super admin changes their own password (at least 10 characters). Returns a fresh login.</summary>
public record ChangeSuperAdminPasswordRequest(string CurrentPassword, string NewPassword);

public class ChangeSuperAdminPasswordRequestValidator : FluentValidation.AbstractValidator<ChangeSuperAdminPasswordRequest>
{
    public ChangeSuperAdminPasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(10).WithMessage("Use at least 10 characters.").MaximumLength(100)
            .NotEqual(x => x.CurrentPassword).WithMessage("The new password must be different from the current one.");
    }
}

public record TwoFactorStatusDto(bool Enabled, int RecoveryCodesLeft);

/// <summary>Shown once while setting up: scan the QR (PNG data URL) or type the secret into the app.</summary>
public record TwoFactorSetupDto(string Secret, string OtpAuthUri, string QrPngDataUrl);

public record EnableTwoFactorRequest(string Code);

/// <summary>The recovery codes, shown only this once.</summary>
public record TwoFactorEnabledDto(List<string> RecoveryCodes);

/// <summary>Turning it off needs the password and a current code (or a recovery code).</summary>
public record DisableTwoFactorRequest(string Password, string Code);

public interface ISuperAdminTwoFactorService
{
    Task<SuperAdminAuthResponse> VerifyLoginAsync(VerifyTwoFactorRequest request, CancellationToken ct = default);
    Task<TwoFactorStatusDto> GetStatusAsync(Guid adminId, CancellationToken ct = default);
    Task<TwoFactorSetupDto> StartSetupAsync(Guid adminId, CancellationToken ct = default);
    Task<TwoFactorEnabledDto> EnableAsync(Guid adminId, EnableTwoFactorRequest request, CancellationToken ct = default);
    Task<TwoFactorEnabledDto> RegenerateRecoveryCodesAsync(Guid adminId, EnableTwoFactorRequest request, CancellationToken ct = default);
    Task DisableAsync(Guid adminId, DisableTwoFactorRequest request, CancellationToken ct = default);
}

/// <summary>Business summary only: the super admin sees counts, never a restaurant's orders or customers.</summary>
public record SuperAdminRestaurantDto(
    Guid Id,
    string Name,
    string Slug,
    string OwnerName,
    string OwnerEmail,
    string? WhatsAppNumber,
    bool IsActive,
    DateTime CreatedAt,
    int OrdersCount,
    DateTime? LastOrderAt,
    string Plan,
    string PlanName,
    string PlanStatus,
    DateTime? PlanExpiresAt);

public record PagedResult<T>(List<T> Items, int Total, int Page, int PageSize);

/// <summary>
/// One plan payment for the super admin's log. Source "online" = Razorpay checkout (Status "Paid", or "Not completed"
/// when the owner opened the checkout but did not pay); "manual" = recorded by a super admin (cash, UPI, bank...).
/// </summary>
public record PaymentLogDto(
    Guid Id,
    string Source,
    string Status,
    Guid RestaurantId,
    string RestaurantName,
    string? PlanName,
    decimal Amount,
    string? Method,
    string? GatewayOrderId,
    string? GatewayPaymentId,
    string? Reference,
    string? Note,
    string? PerformedBy,
    DateTime CreatedAt,
    DateTime? PaidAt,
    decimal RefundedAmount = 0,
    string? RefundStatus = null,
    decimal RefundFee = 0);

/// <summary>ReceivedTotal is before refunds; RefundedTotal is what went back (or is on its way).</summary>
public record PaymentLogSummaryDto(decimal ReceivedTotal, int PaidCount, int NotCompletedCount, decimal RefundedTotal = 0, int OpenRefundRequests = 0);

public record PaymentLogResponse(PaymentLogSummaryDto Summary, List<PaymentLogDto> Items);

/// <summary>One message to or from Razorpay for an order, oldest first. Bodies are the raw JSON.</summary>
public record PaymentGatewayLogDto(string Kind, int? StatusCode, string? RequestBody, string? ResponseBody, string? Note, DateTime CreatedAt);

public record SuperAdminStatsDto(
    int TotalRestaurants,
    int ActiveRestaurants,
    int SuspendedRestaurants,
    int NewRestaurantsLast30Days,
    int TotalOrders,
    int OrdersLast30Days,
    int OnTrial,
    int PaidPlans,
    int ExpiringSoon,
    int InGrace,
    int OrderingStopped,
    // Money the platform received for plans (online + recorded by hand). Days and months are Indian time.
    decimal RevenueToday,
    decimal RevenueYesterday,
    decimal RevenueThisMonth,
    decimal RevenueLastMonth,
    decimal RevenueAllTime,
    int PaymentsThisMonth,
    int OrdersToday,
    int OrdersYesterday,
    int RestaurantsOrderingToday,
    List<PlatformDayDto> Last30Days,
    List<PlatformMonthDto> Last12Months,
    List<RecentPaymentDto> RecentPayments,
    List<RenewalDueDto> RenewalsDue,
    List<TopRestaurantDto> TopRestaurants);

/// <summary>One Indian calendar day: plan revenue and payments, new restaurants, customer orders.</summary>
public record PlatformDayDto(DateOnly Date, decimal Revenue, int Payments, int NewRestaurants, int Orders);

/// <summary>Month is "2026-10".</summary>
public record PlatformMonthDto(string Month, decimal Revenue, int Payments);

public record RecentPaymentDto(Guid RestaurantId, string RestaurantName, string? LogoUrl, string? PlanName, decimal Amount, string? Method, DateTime PaidAt);

public record RenewalDueDto(Guid RestaurantId, string RestaurantName, string? LogoUrl, string? PlanName, string Plan, DateTime ExpiresAt);

public record TopRestaurantDto(Guid RestaurantId, string RestaurantName, string? LogoUrl, string Plan, int OrdersLast30Days);

public record SetRestaurantStatusRequest(bool IsActive);

/// <summary>The temporary password is returned only once and never stored in plain text.</summary>
public record ResetOwnerPasswordResponse(string OwnerEmail, string TemporaryPassword);

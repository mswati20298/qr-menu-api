namespace QrMenu.Application.SuperAdmins;

public record SuperAdminLoginRequest(string Email, string Password);

public record SuperAdminAuthResponse(string Token, string Name);

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

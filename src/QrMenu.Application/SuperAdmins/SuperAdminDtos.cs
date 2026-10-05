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
    int OrderingStopped);

public record SetRestaurantStatusRequest(bool IsActive);

/// <summary>The temporary password is returned only once and never stored in plain text.</summary>
public record ResetOwnerPasswordResponse(string OwnerEmail, string TemporaryPassword);

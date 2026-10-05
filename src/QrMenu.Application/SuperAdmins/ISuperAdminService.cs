namespace QrMenu.Application.SuperAdmins;

public interface ISuperAdminService
{
    Task<SuperAdminAuthResponse> LoginAsync(SuperAdminLoginRequest request, CancellationToken ct = default);
    Task<SuperAdminStatsDto> GetStatsAsync(CancellationToken ct = default);

    /// <summary>
    /// status: "active", "suspended" or empty for all.
    /// plan: "trial", "free", "paid", "expiring", "grace", "stopped" (expired or cancelled) or empty for all.
    /// </summary>
    Task<PagedResult<SuperAdminRestaurantDto>> ListRestaurantsAsync(string? search, string? status, string? plan, int page, int pageSize, CancellationToken ct = default);

    Task<SuperAdminRestaurantDto> SetRestaurantStatusAsync(Guid restaurantId, bool isActive, CancellationToken ct = default);
    /// <summary>Gives the owner a new temporary password (shown once) and signs out all their logins.</summary>
    Task<ResetOwnerPasswordResponse> ResetOwnerPasswordAsync(Guid restaurantId, CancellationToken ct = default);
}

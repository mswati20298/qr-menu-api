namespace QrMenu.Application.Restaurants;

public interface IRestaurantService
{
    Task<RestaurantDto> GetAsync(Guid restaurantId, CancellationToken ct = default);
    Task<RestaurantDto> UpdateAsync(Guid restaurantId, UpdateRestaurantRequest request, CancellationToken ct = default);
    Task<List<ScanStatsDto>> GetScanStatsAsync(Guid restaurantId, int days, CancellationToken ct = default);
    Task<DashboardStatsDto> GetDashboardStatsAsync(Guid restaurantId, CancellationToken ct = default);
    Task<RestaurantDto> SetKitchenPinAsync(Guid restaurantId, SetKitchenPinRequest request, CancellationToken ct = default);
}

namespace QrMenu.Application.Backgrounds;

public interface IRestaurantBackgroundService
{
    Task<BackgroundSettingsDto> GetAsync(Guid restaurantId, CancellationToken ct = default);
    Task<BackgroundItemDto> AddAsync(Guid restaurantId, AddBackgroundRequest request, CancellationToken ct = default);
    Task<BackgroundItemDto> UpdateAsync(Guid restaurantId, Guid backgroundId, UpdateBackgroundRequest request, CancellationToken ct = default);
    Task SetModeAsync(Guid restaurantId, SetBackgroundModeRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid restaurantId, Guid backgroundId, CancellationToken ct = default);
}

namespace QrMenu.Application.Items;

public interface IItemService
{
    Task<List<MenuItemDto>> GetAllAsync(Guid restaurantId, CancellationToken ct = default);
    Task<MenuItemDto> CreateAsync(Guid restaurantId, CreateItemRequest request, CancellationToken ct = default);
    Task<MenuItemDto> UpdateAsync(Guid restaurantId, Guid itemId, UpdateItemRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid restaurantId, Guid itemId, CancellationToken ct = default);
    Task<MenuItemDto> SetAvailabilityAsync(Guid restaurantId, Guid itemId, bool isAvailable, CancellationToken ct = default);
    Task ReorderAsync(Guid restaurantId, Guid categoryId, List<Guid> orderedIds, CancellationToken ct = default);
}

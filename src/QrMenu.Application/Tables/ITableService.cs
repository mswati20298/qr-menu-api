namespace QrMenu.Application.Tables;

public interface ITableService
{
    Task<List<TableDto>> GetAllAsync(Guid restaurantId, CancellationToken ct = default);
    Task<TableDto> CreateAsync(Guid restaurantId, CreateTableRequest request, CancellationToken ct = default);
    Task<TableDto> UpdateAsync(Guid restaurantId, Guid tableId, UpdateTableRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid restaurantId, Guid tableId, CancellationToken ct = default);
}

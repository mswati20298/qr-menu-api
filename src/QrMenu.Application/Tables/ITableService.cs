namespace QrMenu.Application.Tables;

public interface ITableService
{
    Task<List<TableDto>> GetAllAsync(Guid restaurantId, CancellationToken ct = default);
    Task<TableDto> CreateAsync(Guid restaurantId, CreateTableRequest request, CancellationToken ct = default);
    Task<TableDto> UpdateAsync(Guid restaurantId, Guid tableId, UpdateTableRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid restaurantId, Guid tableId, CancellationToken ct = default);
    /// <summary>New secret code: the table's old QR (and every session started with it) stops working.</summary>
    Task<TableDto> ResetQrCodeAsync(Guid restaurantId, Guid tableId, CancellationToken ct = default);
}

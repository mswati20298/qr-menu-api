namespace QrMenu.Application.Orders;

public interface IOrderService
{
    Task<OrderDto> CreatePublicOrderAsync(string slug, CreateOrderRequest request, CancellationToken ct = default);
    Task<List<OrderDto>> GetOrdersByPhoneAsync(string slug, string phone, CancellationToken ct = default);
    Task<OrderDto> GetPublicOrderAsync(string slug, Guid orderId, CancellationToken ct = default);
    Task<OrderDto> CancelPublicOrderAsync(string slug, Guid orderId, CancellationToken ct = default);
    Task<OrderDto> ClaimPublicPaymentAsync(string slug, Guid orderId, ClaimPaymentRequest request, CancellationToken ct = default);

    Task<List<OrderDto>> GetAllForOwnerAsync(Guid restaurantId, string? status, CancellationToken ct = default);
    Task<List<OrderDto>> GetNewOrdersSinceAsync(Guid restaurantId, DateTime since, CancellationToken ct = default);
    Task<OrderDto> GetForOwnerAsync(Guid restaurantId, Guid orderId, CancellationToken ct = default);
    Task<OrderDto> UpdateStatusAsync(Guid restaurantId, Guid orderId, string status, CancellationToken ct = default);
    Task<OrderDto> UpdatePaymentAsync(Guid restaurantId, Guid orderId, UpdatePaymentRequest request, CancellationToken ct = default);

    /// <summary>Order entered by staff from the admin panel. Same pricing rules as a guest order.</summary>
    Task<OrderDto> CreateStaffOrderAsync(Guid restaurantId, StaffOrderRequest request, CancellationToken ct = default);
}

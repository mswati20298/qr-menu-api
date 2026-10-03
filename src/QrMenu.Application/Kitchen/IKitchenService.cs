namespace QrMenu.Application.Kitchen;

public interface IKitchenService
{
    /// <summary>Kitchen-screen login with the restaurant's link name and its kitchen PIN.</summary>
    Task<KitchenAuthResponse> LoginAsync(KitchenLoginRequest request, CancellationToken ct = default);

    Task<KitchenBoardDto> GetBoardAsync(Guid restaurantId, CancellationToken ct = default);

    /// <summary>New → Preparing → Served.</summary>
    Task<KitchenOrderDto> AdvanceAsync(Guid restaurantId, Guid orderId, CancellationToken ct = default);

    /// <summary>One step back (undo a mis-tap): Served → Preparing → New.</summary>
    Task<KitchenOrderDto> RevertAsync(Guid restaurantId, Guid orderId, CancellationToken ct = default);
}

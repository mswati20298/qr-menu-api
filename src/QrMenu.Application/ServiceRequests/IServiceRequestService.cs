namespace QrMenu.Application.ServiceRequests;

public interface IServiceRequestService
{
    /// <summary>Customer side. Returns the existing pending request instead of creating a duplicate.</summary>
    Task<ServiceRequestDto> CreatePublicAsync(string slug, CreateServiceRequest request, CancellationToken ct = default);

    /// <summary>Owner side: requests still waiting for staff, oldest first.</summary>
    Task<List<ServiceRequestDto>> GetPendingAsync(Guid restaurantId, CancellationToken ct = default);

    Task CompleteAsync(Guid restaurantId, Guid requestId, CancellationToken ct = default);
}

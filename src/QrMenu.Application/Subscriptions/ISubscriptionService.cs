namespace QrMenu.Application.Subscriptions;

public interface ISubscriptionService
{
    // Super admin. performedBy = the super admin's email, kept in the history.
    Task<SubscriptionDetailsDto> GetDetailsAsync(Guid restaurantId, CancellationToken ct = default);
    Task<SubscriptionDetailsDto> GrantFreeAsync(Guid restaurantId, GrantFreePlanRequest request, string performedBy, CancellationToken ct = default);
    Task<SubscriptionDetailsDto> RecordPaymentAsync(Guid restaurantId, RecordPaymentRequest request, string performedBy, CancellationToken ct = default);
    Task<SubscriptionDetailsDto> ExtendAsync(Guid restaurantId, ExtendPlanRequest request, string performedBy, CancellationToken ct = default);
    Task<SubscriptionDetailsDto> CancelAsync(Guid restaurantId, CancelPlanRequest request, string performedBy, CancellationToken ct = default);

    // Restaurant owner ("My plan").
    Task<OwnerPlanDto> GetOwnerPlanAsync(Guid restaurantId, CancellationToken ct = default);
}

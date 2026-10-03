namespace QrMenu.Application.Subscriptions;

/// <summary>The plan catalog. Create/update/delete are super admin only.</summary>
public interface IPricingPlanService
{
    Task<List<PricingPlanAdminDto>> GetAllAsync(CancellationToken ct = default);
    Task<PricingPlanAdminDto> CreateAsync(SavePricingPlanRequest request, CancellationToken ct = default);
    Task<PricingPlanAdminDto> UpdateAsync(Guid id, SavePricingPlanRequest request, CancellationToken ct = default);

    /// <summary>Only a plan nobody has bought can be deleted; otherwise deactivate it.</summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

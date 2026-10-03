using Microsoft.EntityFrameworkCore;
using QrMenu.Application.Common.Exceptions;
using QrMenu.Application.Subscriptions;
using QrMenu.Domain.Entities;
using QrMenu.Infrastructure.Persistence;

namespace QrMenu.Infrastructure.Services;

public class PricingPlanService(AppDbContext db) : IPricingPlanService
{
    public async Task<List<PricingPlanAdminDto>> GetAllAsync(CancellationToken ct = default)
    {
        return await Project(db.PricingPlans.AsNoTracking().OrderBy(p => p.SortOrder).ThenBy(p => p.Price)).ToListAsync(ct);
    }

    public async Task<PricingPlanAdminDto> CreateAsync(SavePricingPlanRequest request, CancellationToken ct = default)
    {
        await EnsureNameIsFreeAsync(request.Name, null, ct);

        var plan = new PricingPlan { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow };
        Apply(plan, request);
        db.PricingPlans.Add(plan);
        await db.SaveChangesAsync(ct);

        return await GetAsync(plan.Id, ct);
    }

    public async Task<PricingPlanAdminDto> UpdateAsync(Guid id, SavePricingPlanRequest request, CancellationToken ct = default)
    {
        var plan = await db.PricingPlans.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException("Plan not found.");

        await EnsureNameIsFreeAsync(request.Name, id, ct);

        // Price and duration changes only affect future purchases: past payments keep their own snapshot.
        Apply(plan, request);
        await db.SaveChangesAsync(ct);

        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var plan = await db.PricingPlans.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException("Plan not found.");

        var inUse = await db.PlanPayments.AnyAsync(p => p.PricingPlanId == id, ct)
            || await db.Restaurants.AnyAsync(r => r.PricingPlanId == id, ct);
        if (inUse)
        {
            throw new ConflictException("This plan has already been bought, so it cannot be deleted. Make it inactive instead.");
        }

        db.PricingPlans.Remove(plan);
        await db.SaveChangesAsync(ct);
    }

    private async Task<PricingPlanAdminDto> GetAsync(Guid id, CancellationToken ct) =>
        await Project(db.PricingPlans.AsNoTracking().Where(p => p.Id == id)).FirstAsync(ct);

    private async Task EnsureNameIsFreeAsync(string name, Guid? exceptId, CancellationToken ct)
    {
        var trimmed = name.Trim();
        if (await db.PricingPlans.AnyAsync(p => p.Name == trimmed && p.Id != exceptId, ct))
        {
            throw new ConflictException("A plan with this name already exists.");
        }
    }

    private static void Apply(PricingPlan plan, SavePricingPlanRequest request)
    {
        plan.Name = request.Name.Trim();
        plan.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        plan.DurationMonths = request.DurationMonths;
        plan.Price = request.Price;
        plan.IsActive = request.IsActive;
        plan.SortOrder = request.SortOrder;
        plan.UpdatedAt = DateTime.UtcNow;
    }

    private IQueryable<PricingPlanAdminDto> Project(IQueryable<PricingPlan> query)
    {
        return query.Select(p => new PricingPlanAdminDto(
            p.Id, p.Name, p.Description, p.DurationMonths, p.Price, p.IsActive, p.SortOrder,
            db.PlanPayments.Count(pay => pay.PricingPlanId == p.Id && pay.Status == PlanPaymentStatus.Paid),
            p.CreatedAt, p.UpdatedAt));
    }
}

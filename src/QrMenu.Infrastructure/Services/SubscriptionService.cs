using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QrMenu.Application.Common.Exceptions;
using QrMenu.Application.Common.Interfaces;
using QrMenu.Application.Subscriptions;
using QrMenu.Domain.Entities;
using QrMenu.Infrastructure.Persistence;

namespace QrMenu.Infrastructure.Services;

public class SubscriptionService(
    AppDbContext db,
    IOptions<SubscriptionSettings> options,
    IPaymentGateway paymentGateway,
    TimeProvider clock) : ISubscriptionService
{
    private readonly SubscriptionSettings _settings = options.Value;

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<SubscriptionDetailsDto> GetDetailsAsync(Guid restaurantId, CancellationToken ct = default)
    {
        var restaurant = await FindAsync(restaurantId, ct);
        return await BuildDetailsAsync(restaurant, ct);
    }

    public async Task<SubscriptionDetailsDto> GrantFreeAsync(Guid restaurantId, GrantFreePlanRequest request, string performedBy, CancellationToken ct = default)
    {
        var restaurant = await FindAsync(restaurantId, ct);

        restaurant.Plan = SubscriptionPlan.Free;
        restaurant.PricingPlanId = null;
        restaurant.PlanName = "Free";
        restaurant.PlanExpiresAt = request.Lifetime ? null : request.Until!.Value.ToUniversalTime();
        restaurant.PlanCancelledAt = null;

        PlanChanges.AddEvent(db, restaurant, SubscriptionAction.FreeGranted, performedBy, request.Note, Now);
        await db.SaveChangesAsync(ct);
        return await BuildDetailsAsync(restaurant, ct);
    }

    public async Task<SubscriptionDetailsDto> RecordPaymentAsync(Guid restaurantId, RecordPaymentRequest request, string performedBy, CancellationToken ct = default)
    {
        var restaurant = await FindAsync(restaurantId, ct);
        var plan = await db.PricingPlans.FirstOrDefaultAsync(p => p.Id == request.PricingPlanId, ct)
            ?? throw new NotFoundException("Plan not found.");

        PlanChanges.ApplyPaid(
            db, restaurant, plan, plan.DurationMonths * request.Periods, request.Amount,
            Enum.Parse<PaymentMethod>(request.PaymentMethod, ignoreCase: true),
            request.PaymentReference, request.Note, performedBy, _settings.GraceDays, Now);

        await db.SaveChangesAsync(ct);
        return await BuildDetailsAsync(restaurant, ct);
    }

    public async Task<SubscriptionDetailsDto> ExtendAsync(Guid restaurantId, ExtendPlanRequest request, string performedBy, CancellationToken ct = default)
    {
        var restaurant = await FindAsync(restaurantId, ct);
        var until = request.Until.ToUniversalTime();

        if (restaurant.PlanCancelledAt.HasValue)
        {
            throw new ConflictException("This plan was cancelled. Give a free plan or record a payment to start a new one.");
        }

        if (restaurant.PlanExpiresAt is null)
        {
            throw new ConflictException("This plan has no end date, so there is nothing to extend.");
        }

        if (until <= restaurant.PlanExpiresAt.Value)
        {
            throw new ConflictException("The new end date must be after the current end date.");
        }

        restaurant.PlanExpiresAt = until;

        PlanChanges.AddEvent(db, restaurant, SubscriptionAction.Extended, performedBy, request.Note, Now);
        await db.SaveChangesAsync(ct);
        return await BuildDetailsAsync(restaurant, ct);
    }

    public async Task<SubscriptionDetailsDto> CancelAsync(Guid restaurantId, CancelPlanRequest request, string performedBy, CancellationToken ct = default)
    {
        var restaurant = await FindAsync(restaurantId, ct);

        if (restaurant.PlanCancelledAt.HasValue)
        {
            throw new ConflictException("This plan is already cancelled.");
        }

        restaurant.PlanCancelledAt = Now;

        PlanChanges.AddEvent(db, restaurant, SubscriptionAction.Cancelled, performedBy, request.Note, Now);
        await db.SaveChangesAsync(ct);
        return await BuildDetailsAsync(restaurant, ct);
    }

    public async Task<OwnerPlanDto> GetOwnerPlanAsync(Guid restaurantId, CancellationToken ct = default)
    {
        var restaurant = await db.Restaurants.AsNoTracking().FirstOrDefaultAsync(r => r.Id == restaurantId, ct)
            ?? throw new NotFoundException("Restaurant not found.");

        var history = await db.SubscriptionEvents
            .AsNoTracking()
            .Where(e => e.RestaurantId == restaurantId)
            .OrderByDescending(e => e.CreatedAt)
            .Take(50)
            .ToListAsync(ct);

        var plans = await db.PricingPlans
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Price)
            .Select(p => new PricingPlanDto(p.Id, p.Name, p.Description, p.DurationMonths, p.Price, p.IsActive, p.SortOrder))
            .ToListAsync(ct);

        return new OwnerPlanDto(
            SubscriptionRules.Summarize(restaurant, _settings.GraceDays, Now),
            history.Select(e => new OwnerPlanEventDto(
                e.Action.ToString(), SubscriptionRules.DisplayName(e.Plan, e.PlanName), e.ExpiresAt, e.Amount,
                e.PaymentMethod?.ToString(), e.PaymentReference, e.CreatedAt)).ToList(),
            plans,
            paymentGateway.IsConfigured);
    }

    private async Task<Restaurant> FindAsync(Guid restaurantId, CancellationToken ct)
    {
        return await db.Restaurants.FirstOrDefaultAsync(r => r.Id == restaurantId, ct)
            ?? throw new NotFoundException("Restaurant not found.");
    }

    private async Task<SubscriptionDetailsDto> BuildDetailsAsync(Restaurant restaurant, CancellationToken ct)
    {
        var history = await db.SubscriptionEvents
            .AsNoTracking()
            .Where(e => e.RestaurantId == restaurant.Id)
            .OrderByDescending(e => e.CreatedAt)
            .Take(100)
            .ToListAsync(ct);

        return new SubscriptionDetailsDto(
            restaurant.Id,
            restaurant.Name,
            SubscriptionRules.Summarize(restaurant, _settings.GraceDays, Now),
            history.Select(e => new SubscriptionEventDto(
                e.Id, e.Action.ToString(), e.Plan.ToString(), SubscriptionRules.DisplayName(e.Plan, e.PlanName),
                e.ExpiresAt, e.Amount, e.PaymentMethod?.ToString(), e.PaymentReference, e.Note, e.PerformedBy,
                e.CreatedAt)).ToList());
    }
}

using QrMenu.Application.Subscriptions;
using QrMenu.Domain.Entities;
using QrMenu.Infrastructure.Persistence;

namespace QrMenu.Infrastructure.Services;

/// <summary>Plan changes shared by the super admin (offline payments) and the owner's online checkout.</summary>
internal static class PlanChanges
{
    /// <summary>
    /// Puts the restaurant on a paid plan for <paramref name="months"/> months. Renewing a paid plan that is
    /// still running (or in grace) continues from its end date, so no paid days are lost; anything else
    /// (trial, free, expired, cancelled) starts today.
    /// </summary>
    public static SubscriptionEvent ApplyPaid(
        AppDbContext db,
        Restaurant restaurant,
        PricingPlan plan,
        int months,
        decimal amount,
        PaymentMethod method,
        string? reference,
        string? note,
        string performedBy,
        int graceDays,
        DateTime now)
    {
        var status = SubscriptionRules.GetStatus(restaurant, graceDays, now);
        var continuesCurrent = restaurant.Plan == SubscriptionPlan.Paid
            && restaurant.PlanExpiresAt.HasValue
            && SubscriptionRules.CanTakeOrders(status);
        var start = continuesCurrent ? restaurant.PlanExpiresAt!.Value : now;

        restaurant.Plan = SubscriptionPlan.Paid;
        restaurant.PricingPlanId = plan.Id;
        restaurant.PlanName = plan.Name;
        restaurant.PlanExpiresAt = start.AddMonths(months);
        restaurant.PlanCancelledAt = null;

        var entry = AddEvent(db, restaurant, SubscriptionAction.PaymentRecorded, performedBy, note, now);
        entry.Amount = amount;
        entry.PaymentMethod = method;
        entry.PaymentReference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim();
        return entry;
    }

    public static SubscriptionEvent AddEvent(
        AppDbContext db, Restaurant restaurant, SubscriptionAction action, string performedBy, string? note, DateTime now)
    {
        var entry = new SubscriptionEvent
        {
            Id = Guid.NewGuid(),
            RestaurantId = restaurant.Id,
            Action = action,
            Plan = restaurant.Plan,
            PlanName = SubscriptionRules.DisplayName(restaurant.Plan, restaurant.PlanName),
            ExpiresAt = restaurant.PlanExpiresAt,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            PerformedBy = performedBy,
            CreatedAt = now
        };
        db.SubscriptionEvents.Add(entry);
        return entry;
    }
}

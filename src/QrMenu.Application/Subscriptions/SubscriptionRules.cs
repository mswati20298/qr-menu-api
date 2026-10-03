using QrMenu.Domain.Entities;

namespace QrMenu.Application.Subscriptions;

/// <summary>
/// Plan status is never stored; it is worked out from the end date, the cancel date and the grace period:
/// Active (before the end date, or no end date) → Grace (paid plans only: ordering still works) → Expired (ordering off).
/// A trial or free plan has no grace period. Cancelled turns ordering off at once.
/// </summary>
public static class SubscriptionRules
{
    public const string Active = "Active";
    public const string Grace = "Grace";
    public const string Expired = "Expired";
    public const string Cancelled = "Cancelled";

    /// <summary>Only a paid plan gets the grace period; a trial ends exactly on its end date.</summary>
    public static int GraceDaysFor(SubscriptionPlan plan, int graceDays) => plan == SubscriptionPlan.Paid ? graceDays : 0;

    public static string GetStatus(SubscriptionPlan plan, DateTime? expiresAt, DateTime? cancelledAt, int graceDays, DateTime nowUtc)
    {
        if (cancelledAt.HasValue)
        {
            return Cancelled;
        }

        if (expiresAt is null || expiresAt.Value > nowUtc)
        {
            return Active;
        }

        return expiresAt.Value.AddDays(GraceDaysFor(plan, graceDays)) > nowUtc ? Grace : Expired;
    }

    public static string GetStatus(Restaurant restaurant, int graceDays, DateTime nowUtc) =>
        GetStatus(restaurant.Plan, restaurant.PlanExpiresAt, restaurant.PlanCancelledAt, graceDays, nowUtc);

    public static bool CanTakeOrders(string status) => status is Active or Grace;

    public static bool CanTakeOrders(Restaurant restaurant, int graceDays, DateTime nowUtc) =>
        CanTakeOrders(GetStatus(restaurant, graceDays, nowUtc));

    /// <summary>Display name when the restaurant has none stored (older rows).</summary>
    public static string DisplayName(SubscriptionPlan plan, string? planName) =>
        string.IsNullOrWhiteSpace(planName) ? plan.ToString() : planName;

    public static SubscriptionSummaryDto Summarize(Restaurant restaurant, int graceDays, DateTime nowUtc)
    {
        var status = GetStatus(restaurant, graceDays, nowUtc);
        var planGraceDays = GraceDaysFor(restaurant.Plan, graceDays);
        var graceEndsAt = planGraceDays > 0 ? restaurant.PlanExpiresAt?.AddDays(planGraceDays) : null;

        // Days until ordering stops: to the end date while active, to the end of grace while in grace.
        int? daysLeft = status switch
        {
            Active when restaurant.PlanExpiresAt.HasValue => DaysUntil(restaurant.PlanExpiresAt.Value, nowUtc),
            Grace => DaysUntil(graceEndsAt!.Value, nowUtc),
            Expired or Cancelled => 0,
            _ => null
        };

        return new SubscriptionSummaryDto(
            restaurant.Plan.ToString(),
            DisplayName(restaurant.Plan, restaurant.PlanName),
            status,
            restaurant.PlanExpiresAt,
            graceEndsAt,
            restaurant.PlanCancelledAt,
            daysLeft,
            CanTakeOrders(status),
            graceDays);
    }

    private static int DaysUntil(DateTime target, DateTime nowUtc) =>
        Math.Max(0, (int)Math.Ceiling((target - nowUtc).TotalDays));
}

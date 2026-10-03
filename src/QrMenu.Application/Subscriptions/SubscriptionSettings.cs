namespace QrMenu.Application.Subscriptions;

/// <summary>Bound from the "Subscription" config section.</summary>
public class SubscriptionSettings
{
    /// <summary>Days after a paid plan's end during which customers can still order. Trials get none.</summary>
    public int GraceDays { get; set; } = 7;

    /// <summary>Free trial given to a newly registered restaurant. 0 or less = lifetime free.</summary>
    public int TrialDays { get; set; } = 3;

    /// <summary>Plans ending within this many days count as "expiring soon".</summary>
    public int ExpiringSoonDays { get; set; } = 7;
}

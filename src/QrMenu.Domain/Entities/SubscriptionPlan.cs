namespace QrMenu.Domain.Entities;

/// <summary>What kind of plan a restaurant is on. The paid plan's details come from a PricingPlan.</summary>
public enum SubscriptionPlan
{
    /// <summary>The free trial every new restaurant starts with. No grace period after it ends.</summary>
    Trial,

    /// <summary>Given by the super admin, for life or until a date.</summary>
    Free,

    /// <summary>Bought from the plan catalog, online by the owner or recorded by the super admin.</summary>
    Paid
}

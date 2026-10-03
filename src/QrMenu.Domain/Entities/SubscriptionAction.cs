namespace QrMenu.Domain.Entities;

public enum SubscriptionAction
{
    /// <summary>Free plan given at sign-up (trial) or migrated for an existing restaurant.</summary>
    TrialStarted,
    FreeGranted,
    PaymentRecorded,
    Extended,
    Cancelled
}

namespace QrMenu.Domain.Entities;

/// <summary>Platform-wide settings the super admin can change from the panel. There is exactly one row (Id = 1).</summary>
public class PlatformSettings
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;

    /// <summary>
    /// Free trial for a newly registered restaurant, in days. 0 = no trial: the restaurant has to buy a plan
    /// before customers can order. Changing it only affects restaurants that register afterwards.
    /// </summary>
    public int TrialDays { get; set; } = 3;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Email of the super admin who last changed the settings.</summary>
    public string? UpdatedBy { get; set; }
}

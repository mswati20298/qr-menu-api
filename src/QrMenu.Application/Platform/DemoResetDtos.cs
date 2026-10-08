using FluentValidation;

namespace QrMenu.Application.Platform;

/// <summary>
/// Demo deployment only. AutoResetDays: wipe and re-seed the sample data every this many days (0 = only with the button).
/// </summary>
public record DemoStatusDto(bool IsDemo, int AutoResetDays, DateTime? LastResetAt, DateTime? NextResetAt);

public record UpdateDemoSettingsRequest(int AutoResetDays);

public class UpdateDemoSettingsRequestValidator : AbstractValidator<UpdateDemoSettingsRequest>
{
    public UpdateDemoSettingsRequestValidator()
    {
        RuleFor(x => x.AutoResetDays).InclusiveBetween(0, 365).WithMessage("Auto reset must be between 0 and 365 days.");
    }
}

public interface IDemoResetService
{
    Task<DemoStatusDto> GetStatusAsync(CancellationToken ct = default);
    Task<DemoStatusDto> UpdateSettingsAsync(UpdateDemoSettingsRequest request, string performedBy, CancellationToken ct = default);

    /// <summary>
    /// Deletes every restaurant and everything belonging to them (orders, menus, photos...), then seeds the sample
    /// restaurant again. Platform data stays: super admins, plans, settings and keys. Refused outside the Demo.
    /// </summary>
    Task<DemoStatusDto> ResetAsync(string performedBy, CancellationToken ct = default);

    /// <summary>Called by the background worker: resets when the auto-reset period has passed.</summary>
    Task<bool> ResetIfDueAsync(CancellationToken ct = default);
}

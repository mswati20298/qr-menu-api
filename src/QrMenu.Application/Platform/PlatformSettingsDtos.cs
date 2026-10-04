using FluentValidation;

namespace QrMenu.Application.Platform;

/// <summary>GraceDays is read-only here (set in configuration); TrialDays can be changed from the panel.</summary>
public record PlatformSettingsDto(int TrialDays, int GraceDays, DateTime UpdatedAt, string? UpdatedBy);

/// <summary>TrialDays: 0 (no trial, pay before taking orders) to 90.</summary>
public record UpdatePlatformSettingsRequest(int TrialDays);

public class UpdatePlatformSettingsRequestValidator : AbstractValidator<UpdatePlatformSettingsRequest>
{
    public UpdatePlatformSettingsRequestValidator()
    {
        RuleFor(x => x.TrialDays).InclusiveBetween(0, 90).WithMessage("Trial days must be between 0 and 90.");
    }
}

public interface IPlatformSettingsService
{
    Task<PlatformSettingsDto> GetAsync(CancellationToken ct = default);
    Task<PlatformSettingsDto> UpdateAsync(UpdatePlatformSettingsRequest request, string performedBy, CancellationToken ct = default);

    /// <summary>Trial length for a restaurant registering now.</summary>
    Task<int> GetTrialDaysAsync(CancellationToken ct = default);
}

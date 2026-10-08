using FluentValidation;

namespace QrMenu.Application.Platform;

/// <summary>
/// One secret key as the panel may see it: never the value itself, only whether it is set, where it comes from
/// ("panel" = saved here, "server" = the .env file, "none") and its last 4 characters.
/// </summary>
public record SecretKeyStatus(bool IsSet, string Source, string? LastFour);

/// <summary>Payment and AI keys in use. The Razorpay key id is public, so it is shown in full.</summary>
public record PlatformKeysDto(
    string? RazorpayKeyId,
    string RazorpayKeyIdSource,
    string RazorpayMode,
    SecretKeyStatus RazorpayKeySecret,
    SecretKeyStatus RazorpayWebhookSecret,
    SecretKeyStatus GeminiApiKey,
    string WebhookUrl,
    DateTime UpdatedAt,
    string? UpdatedBy);

/// <summary>
/// For each key: null = leave it as it is, "" = remove the panel value (the server value applies again),
/// anything else = save this new value.
/// </summary>
public record UpdatePlatformKeysRequest(string? RazorpayKeyId, string? RazorpayKeySecret, string? RazorpayWebhookSecret, string? GeminiApiKey);

/// <summary>Checks a Razorpay key pair. Empty fields fall back to the keys in use now.</summary>
public record TestRazorpayKeysRequest(string? KeyId, string? KeySecret);

public record KeyCheckResultDto(bool Ok, string Message);

public class UpdatePlatformKeysRequestValidator : AbstractValidator<UpdatePlatformKeysRequest>
{
    public UpdatePlatformKeysRequestValidator()
    {
        RuleFor(x => x.RazorpayKeyId)
            .Must(k => string.IsNullOrEmpty(k) || k.StartsWith("rzp_test_", StringComparison.Ordinal) || k.StartsWith("rzp_live_", StringComparison.Ordinal))
            .WithMessage("The Razorpay Key ID starts with rzp_test_ or rzp_live_.")
            .MaximumLength(64);

        RuleFor(x => x.RazorpayKeySecret).Must(NoSpaces).WithMessage("The Key Secret cannot contain spaces.").MaximumLength(256);
        RuleFor(x => x.RazorpayWebhookSecret).Must(NoSpaces).WithMessage("The Webhook Secret cannot contain spaces.").MaximumLength(256);
        RuleFor(x => x.GeminiApiKey).Must(NoSpaces).WithMessage("The Gemini API key cannot contain spaces.").MaximumLength(256);
    }

    private static bool NoSpaces(string? value) => value is null || !value.Any(char.IsWhiteSpace);
}

public interface IPlatformKeysService
{
    Task<PlatformKeysDto> GetAsync(CancellationToken ct = default);
    Task<PlatformKeysDto> UpdateAsync(UpdatePlatformKeysRequest request, string performedBy, CancellationToken ct = default);
    Task<KeyCheckResultDto> TestRazorpayAsync(TestRazorpayKeysRequest request, CancellationToken ct = default);

    /// <summary>Reads the saved keys into memory. Called at startup.</summary>
    Task LoadAsync(CancellationToken ct = default);
}

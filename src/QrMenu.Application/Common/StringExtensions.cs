namespace QrMenu.Application.Common;

/// <summary>Small text clean-ups used across the services (one place, one behaviour).</summary>
public static class StringExtensions
{
    /// <summary>Trimmed text, or null when it is empty or only spaces.</summary>
    public static string? CleanOrNull(this string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Emails are compared trimmed and lower-case everywhere (login, sign-up, uniqueness).</summary>
    public static string NormalizeEmail(this string value) => value.Trim().ToLowerInvariant();
}

using System.Linq;
using FluentValidation;

namespace QrMenu.Application.Common;

public static class ValidationExtensions
{
    private static readonly char[] ForbiddenChars = ['"', '\'', '(', ')', '<', '>', '\\', '`'];

    /// <summary>
    /// Image URLs come from our own upload endpoint ("/uploads/xyz.jpg"), so only accept that
    /// or a plain http(s) link, and nothing that could break out of a CSS url("...") or an HTML attribute.
    /// </summary>
    public static IRuleBuilderOptions<T, string?> MustBeSafeImageUrl<T>(this IRuleBuilder<T, string?> ruleBuilder)
    {
        return ruleBuilder
            .Must(IsSafeImageUrl)
            .WithMessage("Image URL is not valid. Please upload the image again.");
    }

    private static bool IsSafeImageUrl(string? url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return true;
        }

        if (url.Length > 500)
        {
            return false;
        }

        if (url.Any(c => char.IsWhiteSpace(c) || char.IsControl(c) || ForbiddenChars.Contains(c)))
        {
            return false;
        }

        return url.StartsWith("/uploads/", StringComparison.Ordinal)
            || url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }
}

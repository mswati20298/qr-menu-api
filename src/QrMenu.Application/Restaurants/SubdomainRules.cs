using System.Text.RegularExpressions;

namespace QrMenu.Application.Restaurants;

/// <summary>Rules for a restaurant's own address, e.g. "saket" in saket.qrenvo.com.</summary>
public static partial class SubdomainRules
{
    public const int MinLength = 3;
    public const int MaxLength = 30;

    // Names the platform needs for itself, plus ones people could be tricked by.
    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "app", "www", "demo", "api", "admin", "administrator", "superadmin", "super-admin", "kitchen", "staff",
        "login", "signin", "signup", "register", "account", "accounts", "auth", "billing", "pay", "payment", "payments",
        "invoice", "invoices", "dashboard", "static", "assets", "cdn", "media", "uploads", "img", "images",
        "mail", "email", "smtp", "imap", "pop", "ftp", "ns1", "ns2", "dns", "vpn", "status", "health", "monitor",
        "help", "support", "docs", "blog", "news", "about", "contact", "privacy", "terms", "legal", "security",
        "test", "testing", "stage", "staging", "dev", "beta", "preview", "sandbox", "qrenvo", "official", "root"
    };

    public static IReadOnlyCollection<string> ReservedNames => Reserved;

    // Lower-case letters, digits and inner hyphens: a valid DNS label.
    [GeneratedRegex("^[a-z0-9](?:[a-z0-9-]*[a-z0-9])?$")]
    private static partial Regex LabelPattern();

    public static string Normalize(string value) => value.Trim().ToLowerInvariant();

    /// <summary>Null when the name is fine, otherwise the reason shown to the owner.</summary>
    public static string? Problem(string subdomain)
    {
        if (subdomain.Length < MinLength || subdomain.Length > MaxLength)
        {
            return $"Use {MinLength} to {MaxLength} characters.";
        }

        if (!LabelPattern().IsMatch(subdomain) || subdomain.Contains("--"))
        {
            return "Use only lower-case letters, numbers and single hyphens (not at the start or end).";
        }

        return Reserved.Contains(subdomain) ? "This name is reserved. Please choose another." : null;
    }

    /// <summary>
    /// The subdomain part of a request host ("saket" for "saket.qrenvo.com"), or null when the host is the root
    /// domain itself, a reserved name (app., demo., www.), or another domain.
    /// </summary>
    public static string? FromHost(string? host, string rootDomain)
    {
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(rootDomain))
        {
            return null;
        }

        var name = host.Split(':')[0].Trim().TrimEnd('.').ToLowerInvariant();
        var suffix = "." + rootDomain.Trim().TrimStart('.').ToLowerInvariant();
        if (!name.EndsWith(suffix, StringComparison.Ordinal))
        {
            return null;
        }

        var label = name[..^suffix.Length];
        // Only one level: "a.b.qrenvo.com" is not a restaurant.
        return label.Length > 0 && !label.Contains('.') && Problem(label) is null ? label : null;
    }
}

namespace QrMenu.Domain.Entities;

/// <summary>Brand colours a restaurant can pick for its menu and admin panel (the UI maps each key to a palette).</summary>
public static class ThemeColors
{
    public const string Default = "masala";

    public static readonly string[] All =
    [
        "masala", "ocean", "purple", "emerald", "sunset", "ruby", "rose", "cyan", "amber"
    ];

    public static bool IsValid(string? key) =>
        key is not null && All.Contains(key.ToLowerInvariant());
}

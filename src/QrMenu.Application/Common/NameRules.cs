using System.Text.RegularExpressions;

namespace QrMenu.Application.Common;

/// <summary>
/// How names of menu things (categories, dishes, sizes, add-ons, table numbers) are tidied and compared:
/// trimmed, inner runs of spaces made single, and compared ignoring upper/lower case.
/// So "Paneer  Tikka " and "paneer tikka" count as the same dish.
/// </summary>
public static partial class NameRules
{
    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    public static string Normalize(string? name) => Spaces().Replace(name?.Trim() ?? string.Empty, " ");
}

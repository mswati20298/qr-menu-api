using QrMenu.Application.Common;

namespace QrMenu.Application.Restaurants;

/// <summary>The public address of a restaurant's menu, used for QR codes and "View live menu".</summary>
public static class MenuLinks
{
    /// <summary>https://saket.qrenvo.com when the restaurant has a subdomain, otherwise {AppUrl}/m/{slug}.</summary>
    public static string MenuUrl(SiteSettings site, string slug, string? subdomain) =>
        site.SubdomainsEnabled && !string.IsNullOrWhiteSpace(subdomain)
            ? $"https://{subdomain}.{site.RootDomain.Trim().TrimStart('.')}"
            : $"{site.AppUrl.TrimEnd('/')}/m/{slug}";

    /// <summary>The address printed on a table's QR card.</summary>
    public static string TableUrl(SiteSettings site, string slug, string? subdomain, string tableNumber)
    {
        var menu = MenuUrl(site, slug, subdomain);
        var separator = site.SubdomainsEnabled && !string.IsNullOrWhiteSpace(subdomain) ? "/?t=" : "?t=";
        return $"{menu}{separator}{Uri.EscapeDataString(tableNumber)}";
    }
}

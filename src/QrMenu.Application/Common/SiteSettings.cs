namespace QrMenu.Application.Common;

/// <summary>
/// Bound from the "Site" config section. The same code runs as two separate deployments, each with its own
/// database: a Demo (demo.qrenvo.com, sample data, test payments) and Prod (app.qrenvo.com + restaurant subdomains).
/// </summary>
public class SiteSettings
{
    /// <summary>"Prod" or "Demo". Shown to people only as a "Demo" ribbon.</summary>
    public string Environment { get; set; } = "Prod";

    /// <summary>
    /// Domain under which restaurants get their own address, e.g. "qrenvo.com" gives "saket.qrenvo.com".
    /// Empty = no restaurant subdomains (local development and the Demo deployment).
    /// </summary>
    public string RootDomain { get; set; } = string.Empty;

    /// <summary>Where the owner panel, kitchen screen and super admin live, e.g. "https://app.qrenvo.com".</summary>
    public string AppUrl { get; set; } = "http://localhost:4200";

    /// <summary>WhatsApp number (country code + digits) owners contact for help, e.g. a forgotten password.</summary>
    public string SupportWhatsApp { get; set; } = string.Empty;

    public bool IsDemo => string.Equals(Environment, "Demo", StringComparison.OrdinalIgnoreCase);

    public bool SubdomainsEnabled => !string.IsNullOrWhiteSpace(RootDomain);
}

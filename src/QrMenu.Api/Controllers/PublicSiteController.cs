using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using QrMenu.Application.Common;
using QrMenu.Application.Restaurants;

namespace QrMenu.Api.Controllers;

/// <summary>What this deployment is (Demo or Prod) and, on a restaurant subdomain, which restaurant it belongs to.</summary>
public record PublicSiteDto(string Environment, bool IsDemo, string? RootDomain, string AppUrl, string? RestaurantSlug);

[ApiController]
[Route("api/public/site")]
public class PublicSiteController(IOptions<SiteSettings> siteOptions, IRestaurantService restaurantService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PublicSiteDto>> Get(CancellationToken ct)
    {
        var site = siteOptions.Value;
        string? slug = null;

        if (site.SubdomainsEnabled)
        {
            // The web app calls the API on its own host, so this is the browser's host (saket.qrenvo.com).
            var subdomain = SubdomainRules.FromHost(Request.Host.Host, site.RootDomain);
            if (subdomain is not null)
            {
                slug = await restaurantService.FindSlugBySubdomainAsync(subdomain, ct);
            }
        }

        return Ok(new PublicSiteDto(
            site.IsDemo ? "Demo" : "Prod",
            site.IsDemo,
            site.SubdomainsEnabled ? site.RootDomain : null,
            site.AppUrl.TrimEnd('/'),
            slug));
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QrMenu.Api.Filters;

namespace QrMenu.Api.Controllers;

// "Owner" policy = a token that carries a restaurantId (super admin tokens do not), and
// ActiveRestaurantFilter blocks restaurants the super admin has suspended.
[Authorize(Policy = "Owner")]
[ServiceFilter(typeof(ActiveRestaurantFilter))]
[ApiController]
public abstract class OwnerControllerBase : ControllerBase
{
    protected Guid RestaurantId
    {
        get
        {
            var claim = User.FindFirst("restaurantId")?.Value
                ?? throw new InvalidOperationException("restaurantId claim is missing from the token.");
            return Guid.Parse(claim);
        }
    }
}

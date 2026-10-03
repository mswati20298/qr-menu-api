using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QrMenu.Api.Filters;
using QrMenu.Application.Kitchen;

namespace QrMenu.Api.Controllers;

/// <summary>
/// Kitchen display. Open to a kitchen-screen token (PIN login) and to the owner's token; both carry the
/// restaurantId, so each screen only ever sees its own restaurant. Kitchen tokens are refused everywhere else.
/// </summary>
[ApiController]
[Route("api/kitchen")]
[Authorize(Policy = "Kitchen")]
[ServiceFilter(typeof(ActiveRestaurantFilter))]
public class KitchenController(IKitchenService kitchenService) : ControllerBase
{
    private Guid RestaurantId => Guid.Parse(User.FindFirst("restaurantId")?.Value
        ?? throw new InvalidOperationException("restaurantId claim is missing from the token."));

    [AllowAnonymous]
    [HttpPost("auth/login")]
    public async Task<ActionResult<KitchenAuthResponse>> Login(KitchenLoginRequest request, CancellationToken ct)
    {
        return Ok(await kitchenService.LoginAsync(request, ct));
    }

    [HttpGet("orders")]
    public async Task<ActionResult<KitchenBoardDto>> Board(CancellationToken ct)
    {
        return Ok(await kitchenService.GetBoardAsync(RestaurantId, ct));
    }

    [HttpPost("orders/{id:guid}/advance")]
    public async Task<ActionResult<KitchenOrderDto>> Advance(Guid id, CancellationToken ct)
    {
        return Ok(await kitchenService.AdvanceAsync(RestaurantId, id, ct));
    }

    [HttpPost("orders/{id:guid}/revert")]
    public async Task<ActionResult<KitchenOrderDto>> Revert(Guid id, CancellationToken ct)
    {
        return Ok(await kitchenService.RevertAsync(RestaurantId, id, ct));
    }
}

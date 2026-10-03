using Microsoft.AspNetCore.Mvc;
using QrMenu.Application.Backgrounds;

namespace QrMenu.Api.Controllers;

[Route("api/backgrounds")]
public class BackgroundsController(IRestaurantBackgroundService backgroundService) : OwnerControllerBase
{
    [HttpGet]
    public async Task<ActionResult<BackgroundSettingsDto>> Get(CancellationToken ct)
    {
        var result = await backgroundService.GetAsync(RestaurantId, ct);
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<BackgroundItemDto>> Add(AddBackgroundRequest request, CancellationToken ct)
    {
        var result = await backgroundService.AddAsync(RestaurantId, request, ct);
        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<BackgroundItemDto>> Update(Guid id, UpdateBackgroundRequest request, CancellationToken ct)
    {
        var result = await backgroundService.UpdateAsync(RestaurantId, id, request, ct);
        return Ok(result);
    }

    [HttpPut("mode")]
    public async Task<IActionResult> SetMode(SetBackgroundModeRequest request, CancellationToken ct)
    {
        await backgroundService.SetModeAsync(RestaurantId, request, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await backgroundService.DeleteAsync(RestaurantId, id, ct);
        return NoContent();
    }
}

using Microsoft.AspNetCore.Mvc;
using QrMenu.Application.Items;

namespace QrMenu.Api.Controllers;

[Route("api/items")]
public class ItemsController(IItemService itemService) : OwnerControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<MenuItemDto>>> GetAll(CancellationToken ct)
    {
        var result = await itemService.GetAllAsync(RestaurantId, ct);
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<MenuItemDto>> Create(CreateItemRequest request, CancellationToken ct)
    {
        var result = await itemService.CreateAsync(RestaurantId, request, ct);
        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<MenuItemDto>> Update(Guid id, UpdateItemRequest request, CancellationToken ct)
    {
        var result = await itemService.UpdateAsync(RestaurantId, id, request, ct);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await itemService.DeleteAsync(RestaurantId, id, ct);
        return NoContent();
    }

    [HttpPatch("{id:guid}/availability")]
    public async Task<ActionResult<MenuItemDto>> SetAvailability(Guid id, UpdateAvailabilityRequest request, CancellationToken ct)
    {
        var result = await itemService.SetAvailabilityAsync(RestaurantId, id, request.IsAvailable, ct);
        return Ok(result);
    }

    [HttpPost("reorder")]
    public async Task<IActionResult> Reorder([FromQuery] Guid categoryId, [FromBody] List<Guid> orderedIds, CancellationToken ct)
    {
        await itemService.ReorderAsync(RestaurantId, categoryId, orderedIds, ct);
        return NoContent();
    }
}

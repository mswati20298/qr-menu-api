using Microsoft.AspNetCore.Mvc;
using QrMenu.Application.Tables;

namespace QrMenu.Api.Controllers;

[Route("api/tables")]
public class TablesController(ITableService tableService) : OwnerControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<TableDto>>> GetAll(CancellationToken ct)
    {
        var result = await tableService.GetAllAsync(RestaurantId, ct);
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<TableDto>> Create(CreateTableRequest request, CancellationToken ct)
    {
        var result = await tableService.CreateAsync(RestaurantId, request, ct);
        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TableDto>> Update(Guid id, UpdateTableRequest request, CancellationToken ct)
    {
        var result = await tableService.UpdateAsync(RestaurantId, id, request, ct);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await tableService.DeleteAsync(RestaurantId, id, ct);
        return NoContent();
    }
}

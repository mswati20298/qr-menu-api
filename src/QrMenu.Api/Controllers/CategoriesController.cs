using Microsoft.AspNetCore.Mvc;
using QrMenu.Application.Categories;

namespace QrMenu.Api.Controllers;

[Route("api/categories")]
public class CategoriesController(ICategoryService categoryService) : OwnerControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<CategoryDto>>> GetAll(CancellationToken ct)
    {
        var result = await categoryService.GetAllAsync(RestaurantId, ct);
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<CategoryDto>> Create(CreateCategoryRequest request, CancellationToken ct)
    {
        var result = await categoryService.CreateAsync(RestaurantId, request, ct);
        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CategoryDto>> Update(Guid id, UpdateCategoryRequest request, CancellationToken ct)
    {
        var result = await categoryService.UpdateAsync(RestaurantId, id, request, ct);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await categoryService.DeleteAsync(RestaurantId, id, ct);
        return NoContent();
    }

    [HttpPost("reorder")]
    public async Task<IActionResult> Reorder(ReorderRequest request, CancellationToken ct)
    {
        await categoryService.ReorderAsync(RestaurantId, request, ct);
        return NoContent();
    }
}

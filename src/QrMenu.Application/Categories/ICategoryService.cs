namespace QrMenu.Application.Categories;

public interface ICategoryService
{
    Task<List<CategoryDto>> GetAllAsync(Guid restaurantId, CancellationToken ct = default);
    Task<CategoryDto> CreateAsync(Guid restaurantId, CreateCategoryRequest request, CancellationToken ct = default);
    Task<CategoryDto> UpdateAsync(Guid restaurantId, Guid categoryId, UpdateCategoryRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid restaurantId, Guid categoryId, CancellationToken ct = default);
    Task ReorderAsync(Guid restaurantId, ReorderRequest request, CancellationToken ct = default);
}

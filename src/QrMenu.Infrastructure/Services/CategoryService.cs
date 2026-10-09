using Microsoft.EntityFrameworkCore;
using QrMenu.Application.Common;
using QrMenu.Application.Categories;
using QrMenu.Application.Common.Exceptions;
using QrMenu.Domain.Entities;
using QrMenu.Infrastructure.Persistence;

namespace QrMenu.Infrastructure.Services;

public class CategoryService(AppDbContext db) : ICategoryService
{
    public async Task<List<CategoryDto>> GetAllAsync(Guid restaurantId, CancellationToken ct = default)
    {
        return await db.Categories
            .Where(c => c.RestaurantId == restaurantId)
            .OrderBy(c => c.SortOrder)
            .Select(c => new CategoryDto(c.Id, c.Name, c.SortOrder, c.MenuItems.Count))
            .ToListAsync(ct);
    }

    public async Task<CategoryDto> CreateAsync(Guid restaurantId, CreateCategoryRequest request, CancellationToken ct = default)
    {
        var name = NameRules.Normalize(request.Name);
        await DuplicateGuard.CategoryNameIsFreeAsync(db, restaurantId, name, null, ct);

        var maxSort = await db.Categories.Where(c => c.RestaurantId == restaurantId)
            .Select(c => (int?)c.SortOrder).MaxAsync(ct) ?? -1;

        var category = new Category
        {
            Id = Guid.NewGuid(),
            RestaurantId = restaurantId,
            Name = name,
            SortOrder = maxSort + 1
        };

        db.Categories.Add(category);
        await db.SaveChangesAsync(ct);

        return new CategoryDto(category.Id, category.Name, category.SortOrder, 0);
    }

    public async Task<CategoryDto> UpdateAsync(Guid restaurantId, Guid categoryId, UpdateCategoryRequest request, CancellationToken ct = default)
    {
        var category = await GetOwnedCategoryAsync(restaurantId, categoryId, ct);
        var name = NameRules.Normalize(request.Name);
        await DuplicateGuard.CategoryNameIsFreeAsync(db, restaurantId, name, categoryId, ct);
        category.Name = name;
        await db.SaveChangesAsync(ct);

        var itemCount = await db.MenuItems.CountAsync(i => i.CategoryId == categoryId, ct);
        return new CategoryDto(category.Id, category.Name, category.SortOrder, itemCount);
    }

    public async Task DeleteAsync(Guid restaurantId, Guid categoryId, CancellationToken ct = default)
    {
        var category = await GetOwnedCategoryAsync(restaurantId, categoryId, ct);
        db.Categories.Remove(category);
        await db.SaveChangesAsync(ct);
    }

    public async Task ReorderAsync(Guid restaurantId, ReorderRequest request, CancellationToken ct = default)
    {
        var categories = await db.Categories
            .Where(c => c.RestaurantId == restaurantId && request.OrderedIds.Contains(c.Id))
            .ToListAsync(ct);

        for (var i = 0; i < request.OrderedIds.Count; i++)
        {
            var category = categories.FirstOrDefault(c => c.Id == request.OrderedIds[i]);
            if (category is not null)
            {
                category.SortOrder = i;
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task<Category> GetOwnedCategoryAsync(Guid restaurantId, Guid categoryId, CancellationToken ct)
    {
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == categoryId, ct)
            ?? throw new NotFoundException("Category not found.");

        if (category.RestaurantId != restaurantId)
        {
            throw new NotFoundException("Category not found.");
        }

        return category;
    }
}

using Microsoft.EntityFrameworkCore;
using QrMenu.Application.Common;
using QrMenu.Application.Common.Exceptions;
using QrMenu.Application.Items;
using QrMenu.Domain.Entities;
using QrMenu.Infrastructure.Persistence;

namespace QrMenu.Infrastructure.Services;

public class ItemService(AppDbContext db) : IItemService
{
    public async Task<List<MenuItemDto>> GetAllAsync(Guid restaurantId, CancellationToken ct = default)
    {
        var items = await db.MenuItems
            .Include(i => i.Category)
            .Include(i => i.Variants)
            .Include(i => i.AddOns)
            .Where(i => i.Category.RestaurantId == restaurantId)
            .OrderBy(i => i.Category.SortOrder).ThenBy(i => i.SortOrder)
            .ToListAsync(ct);

        return items.Select(ToDto).ToList();
    }

    public async Task<MenuItemDto> CreateAsync(Guid restaurantId, CreateItemRequest request, CancellationToken ct = default)
    {
        var category = await GetOwnedCategoryAsync(restaurantId, request.CategoryId, ct);
        var name = NameRules.Normalize(request.Name);
        await DuplicateGuard.ItemNameIsFreeAsync(db, category.Id, name, null, ct);

        var maxSort = await db.MenuItems.Where(i => i.CategoryId == request.CategoryId)
            .Select(i => (int?)i.SortOrder).MaxAsync(ct) ?? -1;

        var item = new MenuItem
        {
            Id = Guid.NewGuid(),
            CategoryId = category.Id,
            Name = name,
            Description = request.Description,
            Price = request.Price,
            ImageUrl = request.ImageUrl,
            IsVeg = request.IsVeg,
            Tag = request.Tag,
            IsAvailable = true,
            SortOrder = maxSort + 1
        };

        ApplyVariantsAndAddOns(item, request.Variants, request.AddOns);

        db.MenuItems.Add(item);
        await db.SaveChangesAsync(ct);

        item.Category = category;
        return ToDto(item);
    }

    public async Task<MenuItemDto> UpdateAsync(Guid restaurantId, Guid itemId, UpdateItemRequest request, CancellationToken ct = default)
    {
        var item = await GetOwnedItemAsync(restaurantId, itemId, ct, includeVariants: true);
        var category = await GetOwnedCategoryAsync(restaurantId, request.CategoryId, ct);

        var name = NameRules.Normalize(request.Name);
        await DuplicateGuard.ItemNameIsFreeAsync(db, category.Id, name, item.Id, ct);
        item.CategoryId = category.Id;
        item.Name = name;
        item.Description = request.Description;
        item.Price = request.Price;
        item.ImageUrl = request.ImageUrl;
        item.IsVeg = request.IsVeg;
        item.Tag = request.Tag;

        item.Variants.Clear();
        item.AddOns.Clear();
        ApplyVariantsAndAddOns(item, request.Variants, request.AddOns);

        await db.SaveChangesAsync(ct);

        item.Category = category;
        return ToDto(item);
    }

    public async Task DeleteAsync(Guid restaurantId, Guid itemId, CancellationToken ct = default)
    {
        var item = await GetOwnedItemAsync(restaurantId, itemId, ct);
        db.MenuItems.Remove(item);
        await db.SaveChangesAsync(ct);
    }

    public async Task<MenuItemDto> SetAvailabilityAsync(Guid restaurantId, Guid itemId, bool isAvailable, CancellationToken ct = default)
    {
        var item = await GetOwnedItemAsync(restaurantId, itemId, ct, includeVariants: true);
        item.IsAvailable = isAvailable;
        await db.SaveChangesAsync(ct);

        var categoryName = await db.Categories.Where(c => c.Id == item.CategoryId).Select(c => c.Name).FirstAsync(ct);
        item.Category = new Category { Id = item.CategoryId, Name = categoryName };
        return ToDto(item);
    }

    public async Task ReorderAsync(Guid restaurantId, Guid categoryId, List<Guid> orderedIds, CancellationToken ct = default)
    {
        await GetOwnedCategoryAsync(restaurantId, categoryId, ct);

        var items = await db.MenuItems
            .Where(i => i.CategoryId == categoryId && orderedIds.Contains(i.Id))
            .ToListAsync(ct);

        for (var i = 0; i < orderedIds.Count; i++)
        {
            var item = items.FirstOrDefault(x => x.Id == orderedIds[i]);
            if (item is not null)
            {
                item.SortOrder = i;
            }
        }

        await db.SaveChangesAsync(ct);
    }

    // Adds new children via the DbSet rather than the parent's navigation collection.
    // With app-generated (Guid) keys, adding through a navigation collection that was
    // just cleared can get the new rows mis-tracked as updates to the removed ones
    // instead of inserts, throwing a spurious DbUpdateConcurrencyException on save.
    // Adding via db.Set<T>().Add(...) unambiguously marks them Added; EF's relationship
    // fixup still populates item.Variants/item.AddOns from the shared MenuItemId.
    private void ApplyVariantsAndAddOns(MenuItem item, List<VariantInput>? variants, List<AddOnInput>? addOns)
    {
        if (variants is { Count: > 0 })
        {
            var hasDefault = variants.Any(v => v.IsDefault);
            for (var i = 0; i < variants.Count; i++)
            {
                var v = variants[i];
                db.ItemVariants.Add(new ItemVariant
                {
                    Id = Guid.NewGuid(),
                    MenuItemId = item.Id,
                    Name = v.Name,
                    Price = v.Price,
                    IsDefault = hasDefault ? v.IsDefault : i == 0,
                    SortOrder = i
                });
            }
        }

        if (addOns is { Count: > 0 })
        {
            for (var i = 0; i < addOns.Count; i++)
            {
                var a = addOns[i];
                db.ItemAddOns.Add(new ItemAddOn
                {
                    Id = Guid.NewGuid(),
                    MenuItemId = item.Id,
                    Name = a.Name,
                    Price = a.Price,
                    SortOrder = i
                });
            }
        }
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

    private async Task<MenuItem> GetOwnedItemAsync(Guid restaurantId, Guid itemId, CancellationToken ct, bool includeVariants = false)
    {
        var query = db.MenuItems.Include(i => i.Category).AsQueryable();
        if (includeVariants)
        {
            query = query.Include(i => i.Variants).Include(i => i.AddOns);
        }

        var item = await query.FirstOrDefaultAsync(i => i.Id == itemId, ct)
            ?? throw new NotFoundException("Item not found.");

        if (item.Category.RestaurantId != restaurantId)
        {
            throw new NotFoundException("Item not found.");
        }

        return item;
    }

    private static MenuItemDto ToDto(MenuItem i) => new(
        i.Id, i.CategoryId, i.Category.Name, i.Name, i.Description, i.Price,
        i.ImageUrl, i.IsVeg, i.Tag, i.IsAvailable, i.SortOrder,
        i.Variants.OrderBy(v => v.SortOrder).Select(v => new ItemVariantDto(v.Id, v.Name, v.Price, v.IsDefault, v.SortOrder)).ToList(),
        i.AddOns.OrderBy(a => a.SortOrder).Select(a => new ItemAddOnDto(a.Id, a.Name, a.Price, a.SortOrder)).ToList());
}

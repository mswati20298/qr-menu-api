using Microsoft.EntityFrameworkCore;
using QrMenu.Application.Common.Exceptions;
using QrMenu.Infrastructure.Persistence;

namespace QrMenu.Infrastructure.Services;

/// <summary>
/// Refuses a second category, dish or table with the same name inside one restaurant (different restaurants may of
/// course use the same names). Names arrive already tidied by NameRules; comparison ignores case. The database has
/// matching unique indexes, so two requests at the same moment cannot slip through either.
/// </summary>
internal static class DuplicateGuard
{
    public static async Task CategoryNameIsFreeAsync(AppDbContext db, Guid restaurantId, string name, Guid? exceptId, CancellationToken ct)
    {
        var lower = name.ToLower();
        if (await db.Categories.AnyAsync(c => c.RestaurantId == restaurantId && c.Id != exceptId && c.Name.ToLower() == lower, ct))
        {
            throw new ConflictException($"There is already a category called \"{name}\".");
        }
    }

    public static async Task ItemNameIsFreeAsync(AppDbContext db, Guid categoryId, string name, Guid? exceptId, CancellationToken ct)
    {
        var lower = name.ToLower();
        if (await db.MenuItems.AnyAsync(i => i.CategoryId == categoryId && i.Id != exceptId && i.Name.ToLower() == lower, ct))
        {
            throw new ConflictException($"\"{name}\" is already in this category.");
        }
    }

    public static async Task TableNumberIsFreeAsync(AppDbContext db, Guid restaurantId, string number, Guid? exceptId, CancellationToken ct)
    {
        var lower = number.ToLower();
        if (await db.Tables.AnyAsync(t => t.RestaurantId == restaurantId && t.Id != exceptId && t.Number.ToLower() == lower, ct))
        {
            throw new ConflictException($"Table \"{number}\" already exists.");
        }
    }
}

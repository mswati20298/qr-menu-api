using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using QrMenu.Domain.Entities;
using QrMenu.Infrastructure.Services;

namespace QrMenu.Infrastructure.Persistence;

/// <summary>
/// Clears the menu cache after any save that touches what guests see on the menu. One place for this, so no
/// service that edits dishes, prices, availability, settings or backgrounds can forget it.
/// </summary>
public class MenuCacheInvalidator(MenuCache menuCache) : SaveChangesInterceptor
{
    private static readonly HashSet<Type> MenuTypes =
        [typeof(Restaurant), typeof(Category), typeof(MenuItem), typeof(ItemVariant), typeof(ItemAddOn), typeof(RestaurantBackground)];

    // Set while saving, read after: once saved, the entries are no longer marked as changed.
    private bool _menuChanged;

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        _menuChanged |= TouchesMenu(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        _menuChanged |= TouchesMenu(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        ClearIfNeeded();
        return result;
    }

    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        ClearIfNeeded();
        return ValueTask.FromResult(result);
    }

    private void ClearIfNeeded()
    {
        if (_menuChanged)
        {
            _menuChanged = false;
            menuCache.Clear();
        }
    }

    private static bool TouchesMenu(DbContext? context) =>
        context is not null && context.ChangeTracker.Entries().Any(e =>
            e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted && MenuTypes.Contains(e.Metadata.ClrType));
}

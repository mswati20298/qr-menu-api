using Microsoft.Extensions.Caching.Memory;
using QrMenu.Domain.Entities;

namespace QrMenu.Infrastructure.Services;

/// <summary>
/// Keeps each restaurant's menu (restaurant, dishes, sizes, add-ons, backgrounds) in memory for a minute, so a full
/// restaurant scanning the same menu costs one database read instead of one per guest. Any saved change to menu data
/// clears it at once (see <see cref="MenuCacheInvalidator"/>), so owners never see an old menu after an edit.
/// Opening hours and "can take orders" are still worked out on every request.
/// </summary>
public class MenuCache(IMemoryCache cache)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(60);
    private long _generation;

    public sealed record Entry(Restaurant Restaurant, List<RestaurantBackground> Backgrounds);

    public async Task<Entry?> GetOrLoadAsync(string slug, Func<Task<Entry?>> load)
    {
        var key = $"menu:{Interlocked.Read(ref _generation)}:{slug.ToLowerInvariant()}";
        if (cache.TryGetValue(key, out Entry? hit))
        {
            return hit;
        }
        var fresh = await load();
        if (fresh is not null)
        {
            cache.Set(key, fresh, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = Lifetime });
        }
        return fresh;
    }

    /// <summary>Forgets every cached menu (old entries can no longer be found and expire on their own).</summary>
    public void Clear() => Interlocked.Increment(ref _generation);
}

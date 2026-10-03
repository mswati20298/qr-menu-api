using Microsoft.EntityFrameworkCore;
using QrMenu.Application.Backgrounds;
using QrMenu.Application.Common.Exceptions;
using QrMenu.Domain.Entities;
using QrMenu.Infrastructure.Persistence;

namespace QrMenu.Infrastructure.Services;

public class RestaurantBackgroundService(AppDbContext db) : IRestaurantBackgroundService
{
    private const int MaxBackgrounds = 8;

    public async Task<BackgroundSettingsDto> GetAsync(Guid restaurantId, CancellationToken ct = default)
    {
        var restaurant = await GetRestaurantAsync(restaurantId, ct);

        var items = await db.RestaurantBackgrounds
            .Where(b => b.RestaurantId == restaurantId)
            .OrderBy(b => b.SortOrder)
            .ThenBy(b => b.CreatedAt)
            .ToListAsync(ct);

        return new BackgroundSettingsDto(restaurant.BackgroundMode.ToString(), items.Select(ToDto).ToList());
    }

    public async Task<BackgroundItemDto> AddAsync(Guid restaurantId, AddBackgroundRequest request, CancellationToken ct = default)
    {
        var restaurant = await GetRestaurantAsync(restaurantId, ct);

        var existing = await db.RestaurantBackgrounds
            .Where(b => b.RestaurantId == restaurantId)
            .ToListAsync(ct);

        if (existing.Count >= MaxBackgrounds)
        {
            throw new ConflictException($"You can add up to {MaxBackgrounds} background images. Delete one to add another.");
        }

        var background = new RestaurantBackground
        {
            Id = Guid.NewGuid(),
            RestaurantId = restaurantId,
            ImageUrl = request.ImageUrl,
            Slots = 0,
            IsDefault = existing.Count == 0, // the first image becomes the default automatically
            SortOrder = existing.Count == 0 ? 0 : existing.Max(b => b.SortOrder) + 1
        };

        db.RestaurantBackgrounds.Add(background);
        SyncCoverImage(restaurant, existing.Append(background));
        await db.SaveChangesAsync(ct);

        return ToDto(background);
    }

    public async Task<BackgroundItemDto> UpdateAsync(Guid restaurantId, Guid backgroundId, UpdateBackgroundRequest request, CancellationToken ct = default)
    {
        var restaurant = await GetRestaurantAsync(restaurantId, ct);

        var items = await db.RestaurantBackgrounds
            .Where(b => b.RestaurantId == restaurantId)
            .ToListAsync(ct);

        var target = items.FirstOrDefault(b => b.Id == backgroundId)
            ?? throw new NotFoundException("Background image not found.");

        target.Slots = request.Slots;

        // A time slot belongs to a single image: take it away from the others.
        if (request.Slots != 0)
        {
            foreach (var other in items.Where(b => b.Id != backgroundId))
            {
                other.Slots &= ~request.Slots;
            }
        }

        // Making an image the default un-defaults the previous one. Un-defaulting the current
        // default is ignored: one image must always be the default (choose another one instead).
        if (request.IsDefault && !target.IsDefault)
        {
            foreach (var other in items)
            {
                other.IsDefault = false;
            }

            target.IsDefault = true;
        }

        SyncCoverImage(restaurant, items);
        await db.SaveChangesAsync(ct);

        return ToDto(target);
    }

    public async Task SetModeAsync(Guid restaurantId, SetBackgroundModeRequest request, CancellationToken ct = default)
    {
        var restaurant = await GetRestaurantAsync(restaurantId, ct);
        restaurant.BackgroundMode = Enum.Parse<BackgroundMode>(request.Mode, ignoreCase: true);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid restaurantId, Guid backgroundId, CancellationToken ct = default)
    {
        var restaurant = await GetRestaurantAsync(restaurantId, ct);

        var items = await db.RestaurantBackgrounds
            .Where(b => b.RestaurantId == restaurantId)
            .ToListAsync(ct);

        var target = items.FirstOrDefault(b => b.Id == backgroundId)
            ?? throw new NotFoundException("Background image not found.");

        db.RestaurantBackgrounds.Remove(target);

        var remaining = items.Where(b => b.Id != backgroundId).OrderBy(b => b.SortOrder).ToList();
        if (target.IsDefault && remaining.Count > 0)
        {
            remaining[0].IsDefault = true;
        }

        SyncCoverImage(restaurant, remaining);
        await db.SaveChangesAsync(ct);
    }

    private async Task<Restaurant> GetRestaurantAsync(Guid restaurantId, CancellationToken ct)
    {
        return await db.Restaurants.FindAsync([restaurantId], ct)
            ?? throw new NotFoundException("Restaurant not found.");
    }

    // Keeps the legacy Restaurant.CoverImageUrl equal to the default image so anything
    // still reading it (older clients, the public menu fallback) keeps working.
    private static void SyncCoverImage(Restaurant restaurant, IEnumerable<RestaurantBackground> items)
    {
        restaurant.CoverImageUrl = items.FirstOrDefault(b => b.IsDefault)?.ImageUrl;
    }

    private static BackgroundItemDto ToDto(RestaurantBackground b) => new(b.Id, b.ImageUrl, b.Slots, b.IsDefault);
}

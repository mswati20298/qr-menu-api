using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QrMenu.Application.Common.Exceptions;
using QrMenu.Application.PublicMenu;
using QrMenu.Application.Subscriptions;
using QrMenu.Domain.Entities;
using QrMenu.Infrastructure.Persistence;

namespace QrMenu.Infrastructure.Services;

public class PublicMenuService(AppDbContext db, IOptions<SubscriptionSettings> subscriptionOptions) : IPublicMenuService
{
    public async Task<PublicMenuResponse> GetMenuAsync(string slug, CancellationToken ct = default)
    {
        var restaurant = await db.Restaurants
            .Include(r => r.Categories.OrderBy(c => c.SortOrder))
            .ThenInclude(c => c.MenuItems.OrderBy(i => i.SortOrder))
            .ThenInclude(i => i.Variants.OrderBy(v => v.SortOrder))
            .Include(r => r.Categories)
            .ThenInclude(c => c.MenuItems)
            .ThenInclude(i => i.AddOns.OrderBy(a => a.SortOrder))
            .FirstOrDefaultAsync(r => r.Slug == slug && r.IsActive, ct)
            ?? throw new NotFoundException("Restaurant not found.");

        var backgrounds = await db.RestaurantBackgrounds
            .Where(b => b.RestaurantId == restaurant.Id)
            .OrderBy(b => b.SortOrder)
            .ThenBy(b => b.CreatedAt)
            .ToListAsync(ct);

        var now = TimeOnly.FromDateTime(DateTime.Now);
        var open = TimeOnly.FromTimeSpan(restaurant.OpenTime);
        var close = TimeOnly.FromTimeSpan(restaurant.CloseTime);
        var isOpenNow = close > open ? now >= open && now <= close : now >= open || now <= close;

        var restaurantDto = new PublicRestaurantDto(
            restaurant.Name, restaurant.Slug, restaurant.Tagline, restaurant.LogoUrl, restaurant.CoverImageUrl,
            restaurant.WhatsAppNumber, restaurant.OpenTime.ToString(@"hh\:mm"),
            restaurant.CloseTime.ToString(@"hh\:mm"), isOpenNow,
            restaurant.IsGstEnabled, restaurant.GstPercentage,
            restaurant.IsServiceChargeEnabled, restaurant.ServiceChargePercentage,
            restaurant.ShowWelcomeMessage, restaurant.WelcomeMessage,
            restaurant.BackgroundMode.ToString(),
            backgrounds.Select(b => new PublicBackgroundDto(b.ImageUrl, b.Slots, b.IsDefault)).ToList(),
            restaurant.ThemeColor,
            SubscriptionRules.CanTakeOrders(restaurant, subscriptionOptions.Value.GraceDays, DateTime.UtcNow),
            restaurant.UpiId,
            restaurant.UpiPayeeName);

        var categories = restaurant.Categories.Select(c => new PublicCategoryDto(
            c.Id, c.Name, c.SortOrder,
            c.MenuItems.Select(i => new PublicMenuItemDto(
                i.Id, i.Name, i.Description, i.Price, i.ImageUrl, i.IsVeg, i.Tag, i.IsAvailable,
                i.Variants.Select(v => new PublicItemVariantDto(v.Id, v.Name, v.Price, v.IsDefault)).ToList(),
                i.AddOns.Select(a => new PublicItemAddOnDto(a.Id, a.Name, a.Price)).ToList()
            )).ToList()
        )).Where(c => c.Items.Count > 0).ToList();

        return new PublicMenuResponse(restaurantDto, categories);
    }

    public async Task LogScanAsync(string slug, string? table, CancellationToken ct = default)
    {
        var restaurant = await db.Restaurants.FirstOrDefaultAsync(r => r.Slug == slug, ct)
            ?? throw new NotFoundException("Restaurant not found.");

        db.ScanLogs.Add(new ScanLog
        {
            Id = Guid.NewGuid(),
            RestaurantId = restaurant.Id,
            TableNumber = table,
            ScannedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync(ct);
    }
}

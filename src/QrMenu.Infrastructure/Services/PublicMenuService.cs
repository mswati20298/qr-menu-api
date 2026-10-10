using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QrMenu.Application.Common;
using QrMenu.Application.Common.Exceptions;
using QrMenu.Application.Common.Interfaces;
using QrMenu.Application.Tables;
using QrMenu.Application.PublicMenu;
using QrMenu.Application.Subscriptions;
using QrMenu.Domain.Entities;
using QrMenu.Infrastructure.Persistence;

namespace QrMenu.Infrastructure.Services;

public class PublicMenuService(
    AppDbContext db,
    IOptions<SubscriptionSettings> subscriptionOptions,
    TimeProvider clock,
    ITableSessionTokens tableSessions,
    MenuCache? menuCache = null) : IPublicMenuService
{
    public async Task<TableSessionDto> StartTableSessionAsync(string slug, StartTableSessionRequest request, CancellationToken ct = default)
    {
        var restaurant = await db.Restaurants.AsNoTracking().FirstOrDefaultAsync(r => r.Slug == slug && r.IsActive, ct)
            ?? throw new NotFoundException("Restaurant not found.");

        var number = request.Table?.Trim();
        var table = await db.Tables.AsNoTracking().FirstOrDefaultAsync(
            t => t.RestaurantId == restaurant.Id && t.Number == number && t.IsActive, ct);

        if (table is null || !TableCodes.Matches(table.QrCode, request.Code))
        {
            throw new ForbiddenException("This QR code is no longer valid. Please ask the staff for help.", "table_qr_invalid");
        }

        var hours = Math.Clamp(restaurant.QrSessionHours, 1, 12);
        var expires = clock.GetUtcNow().UtcDateTime.AddHours(hours);
        return new TableSessionDto(tableSessions.Create(table.Id, table.QrCode, expires), table.Number, expires);
    }

    public async Task<PublicMenuResponse> GetMenuAsync(string slug, CancellationToken ct = default)
    {
        var entry = menuCache is null ? await LoadMenuAsync(slug, ct) : await menuCache.GetOrLoadAsync(slug, () => LoadMenuAsync(slug, ct));
        if (entry is null)
        {
            throw new NotFoundException("Restaurant not found.");
        }
        var (restaurant, backgrounds) = (entry.Restaurant, entry.Backgrounds);

        // Opening hours are Indian time; the server itself runs in UTC.
        var isOpenNow = IndianTime.IsOpen(restaurant.OpenTime, restaurant.CloseTime, clock.GetUtcNow().UtcDateTime);

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
            SubscriptionRules.CanTakeOrders(restaurant, subscriptionOptions.Value.GraceDays, clock.GetUtcNow().UtcDateTime),
            restaurant.UpiId,
            restaurant.UpiPayeeName,
            restaurant.RequireTableQr,
            restaurant.AllowLinkTakeaway);

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

    /// <summary>The menu as stored (no per-request values), or null when there is no such active restaurant.</summary>
    private async Task<MenuCache.Entry?> LoadMenuAsync(string slug, CancellationToken ct)
    {
        // Split query: categories, dishes, sizes and add-ons as separate small queries. As one query the rows multiply
        // (every dish x every size x every add-on) and a normal menu took seconds, sometimes timing out.
        var restaurant = await db.Restaurants
            .AsNoTracking()
            .AsSplitQuery()
            .Include(r => r.Categories.OrderBy(c => c.SortOrder))
            .ThenInclude(c => c.MenuItems.OrderBy(i => i.SortOrder))
            .ThenInclude(i => i.Variants.OrderBy(v => v.SortOrder))
            .Include(r => r.Categories)
            .ThenInclude(c => c.MenuItems)
            .ThenInclude(i => i.AddOns.OrderBy(a => a.SortOrder))
            .FirstOrDefaultAsync(r => r.Slug == slug && r.IsActive, ct);
        if (restaurant is null)
        {
            return null;
        }

        var backgrounds = await db.RestaurantBackgrounds
            .AsNoTracking()
            .Where(b => b.RestaurantId == restaurant.Id)
            .OrderBy(b => b.SortOrder)
            .ThenBy(b => b.CreatedAt)
            .ToListAsync(ct);
        return new MenuCache.Entry(restaurant, backgrounds);
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

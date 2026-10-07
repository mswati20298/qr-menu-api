using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QrMenu.Application.Common;
using QrMenu.Application.Common.Exceptions;
using QrMenu.Application.Common.Interfaces;
using QrMenu.Application.Restaurants;
using QrMenu.Domain.Entities;
using QrMenu.Infrastructure.Persistence;

namespace QrMenu.Infrastructure.Services;

public class RestaurantService(
    AppDbContext db,
    IPasswordHasher passwordHasher,
    IOptions<SiteSettings> siteOptions,
    TimeProvider clock) : IRestaurantService
{
    private readonly SiteSettings _site = siteOptions.Value;

    public async Task<RestaurantDto> GetAsync(Guid restaurantId, CancellationToken ct = default)
    {
        var restaurant = await db.Restaurants.FindAsync([restaurantId], ct)
            ?? throw new NotFoundException("Restaurant not found.");

        return ToDto(restaurant);
    }

    public async Task<RestaurantDto> UpdateAsync(Guid restaurantId, UpdateRestaurantRequest request, CancellationToken ct = default)
    {
        var restaurant = await db.Restaurants.FindAsync([restaurantId], ct)
            ?? throw new NotFoundException("Restaurant not found.");

        restaurant.Name = request.Name;
        restaurant.Tagline = request.Tagline;
        restaurant.Address = request.Address;
        restaurant.Phone = request.Phone;
        restaurant.WhatsAppNumber = request.WhatsAppNumber;
        restaurant.OpenTime = TimeSpan.Parse(request.OpenTime);
        restaurant.CloseTime = TimeSpan.Parse(request.CloseTime);
        restaurant.LogoUrl = request.LogoUrl;
        // CoverImageUrl is managed by RestaurantBackgroundService (default background), so it is
        // intentionally not taken from this request.
        restaurant.IsGstEnabled = request.IsGstEnabled;
        restaurant.GstPercentage = request.GstPercentage;
        restaurant.IsServiceChargeEnabled = request.IsServiceChargeEnabled;
        restaurant.ServiceChargePercentage = request.ServiceChargePercentage;
        restaurant.ShowWelcomeMessage = request.ShowWelcomeMessage;
        restaurant.WelcomeMessage = request.WelcomeMessage;

        // Older clients don't send a colour: leave the current one alone.
        if (request.ThemeColor is not null)
        {
            restaurant.ThemeColor = request.ThemeColor.ToLowerInvariant();
        }

        restaurant.GstNumber = string.IsNullOrWhiteSpace(request.GstNumber) ? null : request.GstNumber.Trim().ToUpperInvariant();
        if (!string.IsNullOrWhiteSpace(request.InvoicePrefix))
        {
            restaurant.InvoicePrefix = request.InvoicePrefix.Trim().ToUpperInvariant();
        }

        // Older clients don't send these: leave them as they are.
        if (request.RequireTableQr is not null)
        {
            restaurant.RequireTableQr = request.RequireTableQr.Value;
        }

        if (request.QrSessionHours is not null)
        {
            restaurant.QrSessionHours = Math.Clamp(request.QrSessionHours.Value, 1, 12);
        }

        if (request.AllowLinkTakeaway is not null)
        {
            restaurant.AllowLinkTakeaway = request.AllowLinkTakeaway.Value;
        }

        restaurant.UpiId = string.IsNullOrWhiteSpace(request.UpiId) ? null : request.UpiId.Trim();
        restaurant.UpiPayeeName = restaurant.UpiId is null || string.IsNullOrWhiteSpace(request.UpiPayeeName)
            ? null
            : request.UpiPayeeName.Trim();

        await db.SaveChangesAsync(ct);
        return ToDto(restaurant);
    }

    public async Task<RestaurantDto> SetSubdomainAsync(Guid restaurantId, SetSubdomainRequest request, CancellationToken ct = default)
    {
        if (!_site.SubdomainsEnabled)
        {
            throw new ConflictException("Own web addresses are not available here.");
        }

        var restaurant = await db.Restaurants.FindAsync([restaurantId], ct)
            ?? throw new NotFoundException("Restaurant not found.");

        if (string.IsNullOrWhiteSpace(request.Subdomain))
        {
            restaurant.Subdomain = null;
        }
        else
        {
            var subdomain = SubdomainRules.Normalize(request.Subdomain);
            var problem = SubdomainRules.Problem(subdomain);
            if (problem is not null)
            {
                throw new ConflictException(problem);
            }

            if (await db.Restaurants.AnyAsync(r => r.Subdomain == subdomain && r.Id != restaurantId, ct))
            {
                throw new ConflictException($"{subdomain}.{_site.RootDomain} is already taken. Please choose another.");
            }

            restaurant.Subdomain = subdomain;
        }

        await db.SaveChangesAsync(ct);
        return ToDto(restaurant);
    }

    public async Task<string?> FindSlugBySubdomainAsync(string subdomain, CancellationToken ct = default)
    {
        var name = SubdomainRules.Normalize(subdomain);
        return await db.Restaurants.AsNoTracking()
            .Where(r => r.Subdomain == name && r.IsActive)
            .Select(r => r.Slug)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<RestaurantDto> SetKitchenPinAsync(Guid restaurantId, SetKitchenPinRequest request, CancellationToken ct = default)
    {
        var restaurant = await db.Restaurants.FindAsync([restaurantId], ct)
            ?? throw new NotFoundException("Restaurant not found.");

        restaurant.KitchenPinHash = string.IsNullOrEmpty(request.Pin) ? null : passwordHasher.Hash(request.Pin);
        // Any kitchen screen signed in with the old PIN is signed out.
        restaurant.KitchenPinVersion++;

        await db.SaveChangesAsync(ct);
        return ToDto(restaurant);
    }

    public async Task<List<ScanStatsDto>> GetScanStatsAsync(Guid restaurantId, int days, CancellationToken ct = default)
    {
        // Days are Indian calendar days (the server runs in UTC).
        var today = IndianTime.DateOf(clock.GetUtcNow().UtcDateTime);
        var firstDay = today.AddDays(-(days - 1));
        var since = IndianTime.StartOfDayUtc(firstDay);

        var scans = await db.ScanLogs
            .Where(s => s.RestaurantId == restaurantId && s.ScannedAt >= since)
            .Select(s => s.ScannedAt)
            .ToListAsync(ct);

        var grouped = scans.GroupBy(IndianTime.DateOf).ToDictionary(g => g.Key, g => g.Count());
        return Enumerable.Range(0, days)
            .Select(i => firstDay.AddDays(i))
            .Select(d => new ScanStatsDto(d, grouped.GetValueOrDefault(d, 0)))
            .ToList();
    }

    public async Task<DashboardStatsDto> GetDashboardStatsAsync(Guid restaurantId, CancellationToken ct = default)
    {
        // "Today" and every day below are Indian calendar days, so the numbers roll over at midnight IST.
        var today = IndianTime.DateOf(clock.GetUtcNow().UtcDateTime);
        var since30 = IndianTime.StartOfDayUtc(today.AddDays(-29));

        var orders = await db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .Where(o => o.RestaurantId == restaurantId && o.CreatedAt >= since30 && o.Status != OrderStatus.Cancelled)
            .ToListAsync(ct);

        var byDay = orders.GroupBy(o => IndianTime.DateOf(o.CreatedAt)).ToDictionary(g => g.Key, g => g.ToList());
        List<Order> OnDay(DateOnly day) => byDay.GetValueOrDefault(day) ?? [];

        var todays = OnDay(today);
        var yesterdays = OnDay(today.AddDays(-1));
        var totalOrders = todays.Count;
        var totalRevenue = todays.Sum(o => o.Total);
        var avgOrderValue = totalOrders > 0 ? totalRevenue / totalOrders : 0;
        var totalOrdersYesterday = yesterdays.Count;
        var totalRevenueYesterday = yesterdays.Sum(o => o.Total);
        var avgOrderValueYesterday = totalOrdersYesterday > 0 ? totalRevenueYesterday / totalOrdersYesterday : 0;

        var totalTables = await db.Tables.CountAsync(t => t.RestaurantId == restaurantId && t.IsActive, ct);
        var activeTables = await db.Orders
            .Where(o => o.RestaurantId == restaurantId && o.TableId != null &&
                        (o.Status == OrderStatus.Placed || o.Status == OrderStatus.Preparing || o.Status == OrderStatus.Served))
            .Select(o => o.TableId)
            .Distinct()
            .CountAsync(ct);

        var scanStats = await GetScanStatsAsync(restaurantId, 7, ct);

        List<DailyRevenueDto> Days(int count) => Enumerable.Range(0, count)
            .Select(i => today.AddDays(i - count + 1))
            .Select(d => new DailyRevenueDto(d, OnDay(d).Sum(o => o.Total), OnDay(d).Count))
            .ToList();

        var revenueTodayByHour = Enumerable.Range(0, 24)
            .Select(h =>
            {
                var inHour = todays.Where(o => IndianTime.FromUtc(o.CreatedAt).Hour == h).ToList();
                return new HourlyRevenueDto(h, inHour.Sum(o => o.Total), inHour.Count);
            })
            .ToList();

        var recentOrdersEntities = await db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .Where(o => o.RestaurantId == restaurantId)
            .OrderByDescending(o => o.CreatedAt)
            .Take(5)
            .ToListAsync(ct);

        // Photos for the dish cards: each menu item's current image.
        var menuItemIds = orders.Concat(recentOrdersEntities)
            .SelectMany(o => o.Items)
            .Where(i => i.MenuItemId != null)
            .Select(i => i.MenuItemId!.Value)
            .Distinct()
            .ToList();
        var images = await db.MenuItems
            .Where(m => menuItemIds.Contains(m.Id) && m.ImageUrl != null)
            .Select(m => new { m.Id, m.ImageUrl })
            .ToDictionaryAsync(m => m.Id, m => m.ImageUrl, ct);
        string? ImageOf(OrderItem item) => item.MenuItemId is { } id ? images.GetValueOrDefault(id) : null;

        var last7Start = today.AddDays(-6);
        var topSellingItems = orders
            .Where(o => IndianTime.DateOf(o.CreatedAt) >= last7Start)
            .SelectMany(o => o.Items)
            .GroupBy(i => i.ItemName)
            .Select(g => new TopSellingItemDto(g.Key, g.Sum(i => i.Qty), g.Select(ImageOf).FirstOrDefault(u => u != null)))
            .OrderByDescending(t => t.QtySold)
            .Take(4)
            .ToList();

        var recentOrders = recentOrdersEntities.Select(o => new RecentOrderDto(
            o.Id,
            o.TableNumberSnapshot,
            string.Join(", ", o.Items.Select(i => i.ItemName)),
            o.Total,
            o.Status.ToString(),
            o.CreatedAt,
            o.Items.Select(ImageOf).FirstOrDefault(u => u != null))).ToList();

        return new DashboardStatsDto(
            totalOrders, totalRevenue, avgOrderValue,
            totalOrdersYesterday, totalRevenueYesterday, avgOrderValueYesterday,
            activeTables, totalTables,
            scanStats, Days(7), Days(30), topSellingItems, recentOrders, revenueTodayByHour);
    }

    private RestaurantDto ToDto(Restaurant r) => new(
        r.Id, r.Name, r.Slug, r.Tagline, r.Address, r.Phone, r.WhatsAppNumber,
        r.OpenTime.ToString(@"hh\:mm"), r.CloseTime.ToString(@"hh\:mm"), r.LogoUrl, r.CoverImageUrl, r.IsActive,
        r.IsGstEnabled, r.GstPercentage, r.IsServiceChargeEnabled, r.ServiceChargePercentage,
        r.ShowWelcomeMessage, r.WelcomeMessage, r.ThemeColor,
        r.GstNumber, r.InvoicePrefix, r.UpiId, r.UpiPayeeName, r.KitchenPinHash is not null,
        r.Subdomain, MenuLinks.MenuUrl(_site, r.Slug, r.Subdomain), _site.SubdomainsEnabled,
        _site.SubdomainsEnabled ? _site.RootDomain : null,
        r.RequireTableQr, r.QrSessionHours, r.AllowLinkTakeaway);
}

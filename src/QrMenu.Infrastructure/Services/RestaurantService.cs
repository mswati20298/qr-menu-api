using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QrMenu.Application.Common;
using QrMenu.Application.Common.Exceptions;
using QrMenu.Application.Common.Interfaces;
using QrMenu.Application.Restaurants;
using QrMenu.Domain.Entities;
using QrMenu.Infrastructure.Persistence;

namespace QrMenu.Infrastructure.Services;

public class RestaurantService(AppDbContext db, IPasswordHasher passwordHasher, IOptions<SiteSettings> siteOptions) : IRestaurantService
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
        var since = DateTime.UtcNow.Date.AddDays(-(days - 1));

        var scans = await db.ScanLogs
            .Where(s => s.RestaurantId == restaurantId && s.ScannedAt >= since)
            .ToListAsync(ct);

        var grouped = scans
            .GroupBy(s => DateOnly.FromDateTime(s.ScannedAt))
            .ToDictionary(g => g.Key, g => g.Count());

        var result = new List<ScanStatsDto>();
        for (var i = 0; i < days; i++)
        {
            var date = DateOnly.FromDateTime(since.AddDays(i));
            result.Add(new ScanStatsDto(date, grouped.GetValueOrDefault(date, 0)));
        }

        return result;
    }

    public async Task<DashboardStatsDto> GetDashboardStatsAsync(Guid restaurantId, CancellationToken ct = default)
    {
        var todayStart = DateTime.UtcNow.Date;
        var todayEnd = todayStart.AddDays(1);

        var todaysOrders = await db.Orders
            .Where(o => o.RestaurantId == restaurantId && o.CreatedAt >= todayStart && o.CreatedAt < todayEnd && o.Status != OrderStatus.Cancelled)
            .ToListAsync(ct);

        var totalOrders = todaysOrders.Count;
        var totalRevenue = todaysOrders.Sum(o => o.Total);
        var avgOrderValue = totalOrders > 0 ? totalRevenue / totalOrders : 0;

        var yesterdayStart = todayStart.AddDays(-1);
        var yesterdaysOrders = await db.Orders
            .Where(o => o.RestaurantId == restaurantId && o.CreatedAt >= yesterdayStart && o.CreatedAt < todayStart && o.Status != OrderStatus.Cancelled)
            .ToListAsync(ct);

        var totalOrdersYesterday = yesterdaysOrders.Count;
        var totalRevenueYesterday = yesterdaysOrders.Sum(o => o.Total);
        var avgOrderValueYesterday = totalOrdersYesterday > 0 ? totalRevenueYesterday / totalOrdersYesterday : 0;

        var totalTables = await db.Tables.CountAsync(t => t.RestaurantId == restaurantId && t.IsActive, ct);
        var activeTables = await db.Orders
            .Where(o => o.RestaurantId == restaurantId && o.TableId != null &&
                        (o.Status == OrderStatus.Placed || o.Status == OrderStatus.Preparing || o.Status == OrderStatus.Served))
            .Select(o => o.TableId)
            .Distinct()
            .CountAsync(ct);

        var scanStats = await GetScanStatsAsync(restaurantId, 7, ct);

        var since7 = DateTime.UtcNow.Date.AddDays(-6);
        var recentOrdersRaw = await db.Orders
            .Include(o => o.Items)
            .Where(o => o.RestaurantId == restaurantId && o.CreatedAt >= since7 && o.Status != OrderStatus.Cancelled)
            .ToListAsync(ct);

        var revenueByDate = recentOrdersRaw
            .GroupBy(o => DateOnly.FromDateTime(o.CreatedAt))
            .ToDictionary(g => g.Key, g => (Revenue: g.Sum(o => o.Total), Count: g.Count()));

        var revenueLast7Days = new List<DailyRevenueDto>();
        for (var i = 0; i < 7; i++)
        {
            var date = DateOnly.FromDateTime(since7.AddDays(i));
            var (revenue, count) = revenueByDate.GetValueOrDefault(date, (0, 0));
            revenueLast7Days.Add(new DailyRevenueDto(date, revenue, count));
        }

        var since30 = DateTime.UtcNow.Date.AddDays(-29);
        var ordersLast30 = await db.Orders
            .Where(o => o.RestaurantId == restaurantId && o.CreatedAt >= since30 && o.Status != OrderStatus.Cancelled)
            .Select(o => new { o.CreatedAt, o.Total })
            .ToListAsync(ct);

        var revenueByDate30 = ordersLast30
            .GroupBy(o => DateOnly.FromDateTime(o.CreatedAt))
            .ToDictionary(g => g.Key, g => (Revenue: g.Sum(o => o.Total), Count: g.Count()));

        var revenueLast30Days = new List<DailyRevenueDto>();
        for (var i = 0; i < 30; i++)
        {
            var date = DateOnly.FromDateTime(since30.AddDays(i));
            var (revenue, count) = revenueByDate30.GetValueOrDefault(date, (0, 0));
            revenueLast30Days.Add(new DailyRevenueDto(date, revenue, count));
        }

        var topSellingItems = recentOrdersRaw
            .SelectMany(o => o.Items)
            .GroupBy(i => i.ItemName)
            .Select(g => new TopSellingItemDto(g.Key, g.Sum(i => i.Qty)))
            .OrderByDescending(t => t.QtySold)
            .Take(4)
            .ToList();

        var recentOrdersEntities = await db.Orders
            .Include(o => o.Items)
            .Where(o => o.RestaurantId == restaurantId)
            .OrderByDescending(o => o.CreatedAt)
            .Take(5)
            .ToListAsync(ct);

        var recentOrders = recentOrdersEntities.Select(o => new RecentOrderDto(
            o.Id,
            o.TableNumberSnapshot,
            string.Join(", ", o.Items.Select(i => i.ItemName)),
            o.Total,
            o.Status.ToString(),
            o.CreatedAt)).ToList();

        return new DashboardStatsDto(
            totalOrders, totalRevenue, avgOrderValue,
            totalOrdersYesterday, totalRevenueYesterday, avgOrderValueYesterday,
            activeTables, totalTables,
            scanStats, revenueLast7Days, revenueLast30Days, topSellingItems, recentOrders);
    }

    private RestaurantDto ToDto(Restaurant r) => new(
        r.Id, r.Name, r.Slug, r.Tagline, r.Address, r.Phone, r.WhatsAppNumber,
        r.OpenTime.ToString(@"hh\:mm"), r.CloseTime.ToString(@"hh\:mm"), r.LogoUrl, r.CoverImageUrl, r.IsActive,
        r.IsGstEnabled, r.GstPercentage, r.IsServiceChargeEnabled, r.ServiceChargePercentage,
        r.ShowWelcomeMessage, r.WelcomeMessage, r.ThemeColor,
        r.GstNumber, r.InvoicePrefix, r.UpiId, r.UpiPayeeName, r.KitchenPinHash is not null,
        r.Subdomain, MenuLinks.MenuUrl(_site, r.Slug, r.Subdomain), _site.SubdomainsEnabled,
        _site.SubdomainsEnabled ? _site.RootDomain : null);
}

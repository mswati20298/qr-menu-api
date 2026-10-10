using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using QrMenu.Application.Common;
using QrMenu.Application.Subscriptions;
using QrMenu.Domain.Entities;
using QrMenu.Infrastructure.Auth;
using QrMenu.Infrastructure.Persistence;
using QrMenu.Infrastructure.Services;
using Xunit;

namespace QrMenu.Tests.Services;

public class MenuCacheTests
{
    private readonly string _dbName = Guid.NewGuid().ToString();
    private readonly MenuCache _cache = new(new MemoryCache(new MemoryCacheOptions()));

    /// <summary>A context like the app's: saves clear the menu cache.</summary>
    private AppDbContext AppDb() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(_dbName).AddInterceptors(new MenuCacheInvalidator(_cache)).Options);

    /// <summary>A context that bypasses the interceptor, like a change the cache cannot see.</summary>
    private AppDbContext RawDb() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(_dbName).Options);

    private PublicMenuService Service(AppDbContext db) => new(
        db, Options.Create(new SubscriptionSettings()), TimeProvider.System,
        new TableSessionTokens(Options.Create(new JwtSettings { Secret = new string('x', 64), Issuer = "t", Audience = "t" })), _cache);

    private async Task<Guid> SeedAsync()
    {
        await using var db = AppDb();
        var restaurant = new Restaurant
        {
            Id = Guid.NewGuid(), Name = "Saket Rasoi", Slug = "saket", WhatsAppNumber = "919876543210", IsActive = true,
            Plan = SubscriptionPlan.Free
        };
        var category = new Category { Id = Guid.NewGuid(), RestaurantId = restaurant.Id, Name = "Starters" };
        var item = new MenuItem { Id = Guid.NewGuid(), CategoryId = category.Id, Name = "Paneer Tikka", Price = 200 };
        db.AddRange(restaurant, category, item);
        await db.SaveChangesAsync();
        return item.Id;
    }

    [Fact]
    public async Task RepeatedReads_ComeFromTheCache()
    {
        var itemId = await SeedAsync();
        await using (var db = AppDb())
        {
            (await Service(db).GetMenuAsync("saket")).Categories.Single().Items.Single().Price.Should().Be(200);
        }

        // Changed without the interceptor: the cache does not know, so the cached menu is served.
        await using (var raw = RawDb())
        {
            var item = await raw.MenuItems.FirstAsync(i => i.Id == itemId);
            item.Price = 999;
            await raw.SaveChangesAsync();
        }

        await using var db2 = AppDb();
        (await Service(db2).GetMenuAsync("saket")).Categories.Single().Items.Single().Price.Should().Be(200);
    }

    [Fact]
    public async Task AnEditThroughTheApp_ShowsUpAtOnce()
    {
        var itemId = await SeedAsync();
        await using (var db = AppDb())
        {
            await Service(db).GetMenuAsync("saket");
        }

        await using (var db = AppDb())
        {
            var item = await db.MenuItems.FirstAsync(i => i.Id == itemId);
            item.Price = 260;
            item.IsAvailable = false;
            await db.SaveChangesAsync();
        }

        await using var db3 = AppDb();
        var dish = (await Service(db3).GetMenuAsync("saket")).Categories.Single().Items.Single();
        dish.Price.Should().Be(260);
        dish.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task SavingSomethingElse_KeepsTheCache()
    {
        await SeedAsync();
        await using (var db = AppDb())
        {
            await Service(db).GetMenuAsync("saket");
        }
        var before = await RawDbItemPriceChangeAndRead();

        before.Should().Be(200, "an order or a scan log is not menu data, so the cached menu stays");
    }

    private async Task<decimal> RawDbItemPriceChangeAndRead()
    {
        await using (var raw = RawDb())
        {
            var item = await raw.MenuItems.FirstAsync();
            item.Price = 555;
            await raw.SaveChangesAsync();
        }
        // A save of non-menu data through the app (scan log) must not clear the cache.
        await using (var db = AppDb())
        {
            db.ScanLogs.Add(new ScanLog { Id = Guid.NewGuid(), RestaurantId = (await db.Restaurants.FirstAsync()).Id });
            await db.SaveChangesAsync();
        }
        await using var db2 = AppDb();
        return (await Service(db2).GetMenuAsync("saket")).Categories.Single().Items.Single().Price;
    }
}

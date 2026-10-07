using FluentAssertions;
using Microsoft.Extensions.Options;
using QrMenu.Application.Common;
using QrMenu.Domain.Entities;
using QrMenu.Infrastructure.Auth;
using QrMenu.Infrastructure.Services;
using QrMenu.Tests.Common;
using Xunit;

namespace QrMenu.Tests.Services;

/// <summary>The dashboard counts Indian calendar days, whatever time zone the server runs in.</summary>
public class DashboardStatsTests
{
    private sealed class Clock(DateTime utc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utc, TimeSpan.Zero);
    }

    [Fact]
    public async Task Today_StartsAtMidnightIndianTime()
    {
        var db = InMemoryDbFactory.Create();
        var restaurant = new Restaurant { Id = Guid.NewGuid(), Name = "Saket Rasoi", Slug = "saket-rasoi", WhatsAppNumber = "919876543210" };
        db.Restaurants.Add(restaurant);

        // 6 Oct 23:30 IST (yesterday) and 7 Oct 00:30 IST (today); both are 6 Oct in UTC.
        Order At(DateTime utc, decimal total) => new()
        {
            Id = Guid.NewGuid(), RestaurantId = restaurant.Id, Status = OrderStatus.Served, Total = total, CreatedAt = utc,
            Items = [new OrderItem { Id = Guid.NewGuid(), ItemName = "Paneer Tikka", Qty = 1, UnitPrice = total, LineTotal = total }]
        };
        db.Orders.AddRange(At(new DateTime(2026, 10, 6, 18, 0, 0, DateTimeKind.Utc), 100), At(new DateTime(2026, 10, 6, 19, 0, 0, DateTimeKind.Utc), 250));
        await db.SaveChangesAsync();

        // 7 Oct 02:00 IST.
        var service = new RestaurantService(db, new BcryptPasswordHasher(), Options.Create(new SiteSettings()),
            new Clock(new DateTime(2026, 10, 6, 20, 30, 0, DateTimeKind.Utc)));

        var stats = await service.GetDashboardStatsAsync(restaurant.Id);

        stats.TotalOrdersToday.Should().Be(1);
        stats.TotalRevenueToday.Should().Be(250);
        stats.TotalRevenueYesterday.Should().Be(100);
        stats.RevenueLast7Days[^1].Date.Should().Be(new DateOnly(2026, 10, 7));
        stats.RevenueTodayByHour.Should().HaveCount(24);
        stats.RevenueTodayByHour[0].Revenue.Should().Be(250); // 00:30 IST falls in hour 0
    }
}

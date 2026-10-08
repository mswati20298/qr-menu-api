using FluentAssertions;
using Microsoft.Extensions.Options;
using QrMenu.Application.Subscriptions;
using QrMenu.Domain.Entities;
using QrMenu.Infrastructure.Auth;
using QrMenu.Infrastructure.Services;
using QrMenu.Tests.Common;
using Xunit;

namespace QrMenu.Tests.Services;

/// <summary>The super admin dashboard shows the money the platform received for plans.</summary>
public class SuperAdminStatsTests
{
    [Fact]
    public async Task Revenue_CountsRecordedPlanPayments_ButNotFreePlans()
    {
        var db = InMemoryDbFactory.Create();
        var restaurant = new Restaurant { Id = Guid.NewGuid(), Name = "Saket Rasoi", Slug = "saket-rasoi", WhatsAppNumber = "919876543210" };
        db.Restaurants.Add(restaurant);

        SubscriptionEvent Event(SubscriptionAction action, decimal? amount, DateTime at) => new()
        {
            Id = Guid.NewGuid(), RestaurantId = restaurant.Id, Action = action, Amount = amount, PlanName = "Monthly",
            PaymentMethod = PaymentMethod.Online, PerformedBy = "test", CreatedAt = at
        };
        var now = DateTime.UtcNow;
        db.SubscriptionEvents.AddRange(
            Event(SubscriptionAction.PaymentRecorded, 999, now),
            Event(SubscriptionAction.PaymentRecorded, 5649, now.AddDays(-40)),
            Event(SubscriptionAction.FreeGranted, null, now));
        await db.SaveChangesAsync();

        var service = new SuperAdminService(db, new BcryptPasswordHasher(), null!, Options.Create(new SubscriptionSettings()));
        var stats = await service.GetStatsAsync();

        stats.RevenueToday.Should().Be(999);
        stats.RevenueAllTime.Should().Be(999 + 5649);
        stats.Last30Days.Should().HaveCount(30);
        stats.Last30Days[^1].Revenue.Should().Be(999);
        stats.Last12Months.Should().HaveCount(12);
        stats.Last12Months.Sum(m => m.Revenue).Should().Be(999 + 5649);
        stats.RecentPayments.Should().HaveCount(2);
        stats.RecentPayments[0].Amount.Should().Be(999);
        stats.RecentPayments[0].RestaurantName.Should().Be("Saket Rasoi");
    }
}

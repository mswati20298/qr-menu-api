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

        var service = new SuperAdminService(db, new BcryptPasswordHasher(), null!, Options.Create(new SubscriptionSettings()), null!);
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

    [Fact]
    public async Task PaymentLog_ShowsCheckoutsAndManualPayments_WithoutCountingOnlineTwice()
    {
        var db = InMemoryDbFactory.Create();
        var restaurant = new Restaurant { Id = Guid.NewGuid(), Name = "Saket Rasoi", Slug = "saket-rasoi", WhatsAppNumber = "919876543210" };
        var plan = new PricingPlan { Id = Guid.NewGuid(), Name = "Monthly", Price = 999, DurationMonths = 1 };
        db.Restaurants.Add(restaurant);
        db.PricingPlans.Add(plan);
        var now = DateTime.UtcNow;
        db.PlanPayments.AddRange(
            new PlanPayment { Id = Guid.NewGuid(), RestaurantId = restaurant.Id, PricingPlanId = plan.Id, PlanName = "Monthly", Amount = 999, Status = PlanPaymentStatus.Paid, GatewayOrderId = "order_paid", GatewayPaymentId = "pay_1", CreatedAt = now.AddMinutes(-10), PaidAt = now.AddMinutes(-9) },
            new PlanPayment { Id = Guid.NewGuid(), RestaurantId = restaurant.Id, PricingPlanId = plan.Id, PlanName = "Monthly", Amount = 999, Status = PlanPaymentStatus.Created, GatewayOrderId = "order_left", CreatedAt = now.AddMinutes(-5) });
        db.SubscriptionEvents.AddRange(
            // The online one also leaves an event: it must not appear twice.
            new SubscriptionEvent { Id = Guid.NewGuid(), RestaurantId = restaurant.Id, Action = SubscriptionAction.PaymentRecorded, Amount = 999, PaymentMethod = PaymentMethod.Online, PerformedBy = "owner (online)", CreatedAt = now.AddMinutes(-9) },
            new SubscriptionEvent { Id = Guid.NewGuid(), RestaurantId = restaurant.Id, Action = SubscriptionAction.PaymentRecorded, Amount = 5649, PlanName = "Half Yearly", PaymentMethod = PaymentMethod.Upi, PaymentReference = "UTR123", PerformedBy = "admin@test.com", CreatedAt = now });
        await db.SaveChangesAsync();

        var service = new SuperAdminService(db, new BcryptPasswordHasher(), null!, Options.Create(new SubscriptionSettings()), null!);
        var log = await service.ListPaymentsAsync(null, null);

        log.Items.Should().HaveCount(3);
        log.Items[0].Source.Should().Be("manual");
        log.Items[0].Method.Should().Be("Upi");
        log.Summary.ReceivedTotal.Should().Be(999 + 5649);
        log.Summary.PaidCount.Should().Be(2);
        log.Summary.NotCompletedCount.Should().Be(1);
        (await service.ListPaymentsAsync("unpaid", null)).Items.Should().ContainSingle(p => p.GatewayOrderId == "order_left");
        (await service.ListPaymentsAsync(null, "UTR123")).Items.Should().ContainSingle();
    }
}

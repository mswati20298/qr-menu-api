using FluentAssertions;
using Microsoft.Extensions.Options;
using QrMenu.Application.Common.Exceptions;
using QrMenu.Application.Common.Interfaces;
using QrMenu.Application.Subscriptions;
using QrMenu.Domain.Entities;
using QrMenu.Infrastructure.Persistence;
using QrMenu.Infrastructure.Services;
using QrMenu.Tests.Common;
using Xunit;

namespace QrMenu.Tests.Services;

public class SubscriptionServiceTests
{
    private static readonly DateTime Now = new(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc);

    private sealed class FixedClock(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }

    private sealed class FakeGateway : IPaymentGateway
    {
        public bool IsConfigured => true;
        public string KeyId => "rzp_test_x";
        public Task<string> CreateOrderAsync(long amountInPaise, string currency, string receipt, IDictionary<string, string> notes, CancellationToken ct = default) =>
            Task.FromResult("order_test");
        public bool IsPaymentSignatureValid(string orderId, string paymentId, string signature) => true;
        public bool IsWebhookSignatureValid(string body, string? signature) => true;
        public Task<GatewayCheckResult> CheckCredentialsAsync(string keyId, string keySecret, CancellationToken ct = default) =>
            Task.FromResult(new GatewayCheckResult(true, "ok"));
    }

    private static SubscriptionService CreateService(out AppDbContext db, out Restaurant restaurant, out PricingPlan monthly, out PricingPlan yearly)
    {
        db = InMemoryDbFactory.Create();
        restaurant = new Restaurant
        {
            Id = Guid.NewGuid(),
            Name = "Saket Rasoi",
            Slug = "saket-rasoi",
            WhatsAppNumber = "919876543210",
            Plan = SubscriptionPlan.Trial,
            PlanName = "Trial",
            PlanExpiresAt = Now.AddDays(2)
        };
        monthly = new PricingPlan { Id = Guid.NewGuid(), Name = "Monthly", DurationMonths = 1, Price = 499 };
        yearly = new PricingPlan { Id = Guid.NewGuid(), Name = "Yearly", DurationMonths = 12, Price = 4999 };
        db.Restaurants.Add(restaurant);
        db.PricingPlans.AddRange(monthly, yearly, new PricingPlan { Id = Guid.NewGuid(), Name = "Old", DurationMonths = 3, Price = 1, IsActive = false });
        db.SaveChanges();

        return new SubscriptionService(db, Options.Create(new SubscriptionSettings { GraceDays = 7 }), new FakeGateway(), new FixedClock(Now));
    }

    [Theory]
    [InlineData(SubscriptionPlan.Paid, 10, null, SubscriptionRules.Active)]
    [InlineData(SubscriptionPlan.Paid, -3, null, SubscriptionRules.Grace)]
    [InlineData(SubscriptionPlan.Paid, -8, null, SubscriptionRules.Expired)]
    [InlineData(SubscriptionPlan.Paid, 10, -1, SubscriptionRules.Cancelled)]
    [InlineData(SubscriptionPlan.Trial, 1, null, SubscriptionRules.Active)]
    [InlineData(SubscriptionPlan.Trial, -1, null, SubscriptionRules.Expired)]
    [InlineData(SubscriptionPlan.Free, -1, null, SubscriptionRules.Expired)]
    public void GetStatus_FollowsEndDateGraceAndCancel(SubscriptionPlan plan, int expiresInDays, int? cancelledDaysAgo, string expected)
    {
        var status = SubscriptionRules.GetStatus(
            plan,
            Now.AddDays(expiresInDays),
            cancelledDaysAgo.HasValue ? Now.AddDays(cancelledDaysAgo.Value) : null,
            graceDays: 7,
            Now);

        status.Should().Be(expected);
        SubscriptionRules.CanTakeOrders(status).Should().Be(expected is SubscriptionRules.Active or SubscriptionRules.Grace);
    }

    [Fact]
    public void GetStatus_NoEndDate_IsAlwaysActive()
    {
        SubscriptionRules.GetStatus(SubscriptionPlan.Free, null, null, 7, Now).Should().Be(SubscriptionRules.Active);
    }

    [Fact]
    public async Task RecordPayment_FromTrial_StartsToday()
    {
        var service = CreateService(out _, out var restaurant, out var monthly, out _);

        var result = await service.RecordPaymentAsync(restaurant.Id,
            new RecordPaymentRequest(monthly.Id, 1, 499, "Upi", "UTR123", null), "admin@test.com");

        result.Current.Plan.Should().Be("Paid");
        result.Current.PlanName.Should().Be("Monthly");
        result.Current.ExpiresAt.Should().Be(Now.AddMonths(1));
        result.History.Should().ContainSingle(e => e.Action == "PaymentRecorded" && e.Amount == 499 && e.PaymentMethod == "Upi");
    }

    [Fact]
    public async Task RecordPayment_RenewingRunningPaidPlan_ContinuesFromCurrentEnd()
    {
        var service = CreateService(out var db, out var restaurant, out var monthly, out var yearly);
        restaurant.Plan = SubscriptionPlan.Paid;
        restaurant.PricingPlanId = monthly.Id;
        restaurant.PlanExpiresAt = Now.AddDays(10);
        await db.SaveChangesAsync();

        var result = await service.RecordPaymentAsync(restaurant.Id,
            new RecordPaymentRequest(yearly.Id, 1, 4999, "BankTransfer", null, null), "admin@test.com");

        result.Current.ExpiresAt.Should().Be(Now.AddDays(10).AddMonths(12));
        result.Current.PlanName.Should().Be("Yearly");
    }

    [Fact]
    public async Task RecordPayment_PeriodsMultiplyPlanDuration()
    {
        var service = CreateService(out _, out var restaurant, out var monthly, out _);

        var result = await service.RecordPaymentAsync(restaurant.Id,
            new RecordPaymentRequest(monthly.Id, 3, 1497, "Cash", null, null), "admin@test.com");

        result.Current.ExpiresAt.Should().Be(Now.AddMonths(3));
    }

    [Fact]
    public async Task RecordPayment_AfterCancel_StartsTodayAndClearsCancel()
    {
        var service = CreateService(out var db, out var restaurant, out var monthly, out _);
        restaurant.Plan = SubscriptionPlan.Paid;
        restaurant.PlanExpiresAt = Now.AddDays(10);
        restaurant.PlanCancelledAt = Now.AddDays(-1);
        await db.SaveChangesAsync();

        var result = await service.RecordPaymentAsync(restaurant.Id,
            new RecordPaymentRequest(monthly.Id, 2, 998, "Cash", null, null), "admin@test.com");

        result.Current.Status.Should().Be(SubscriptionRules.Active);
        result.Current.ExpiresAt.Should().Be(Now.AddMonths(2));
    }

    [Fact]
    public async Task TrialEnded_HasNoGracePeriod()
    {
        var service = CreateService(out var db, out var restaurant, out _, out _);
        restaurant.PlanExpiresAt = Now.AddHours(-1);
        await db.SaveChangesAsync();

        var plan = await service.GetOwnerPlanAsync(restaurant.Id);

        plan.Current.Status.Should().Be(SubscriptionRules.Expired);
        plan.Current.CanTakeOrders.Should().BeFalse();
        plan.Current.GraceEndsAt.Should().BeNull();
    }

    [Fact]
    public async Task GrantFree_Lifetime_RemovesEndDate()
    {
        var service = CreateService(out _, out var restaurant, out _, out _);

        var result = await service.GrantFreeAsync(restaurant.Id, new GrantFreePlanRequest(true, null, "Partner"), "admin@test.com");

        result.Current.Plan.Should().Be("Free");
        result.Current.ExpiresAt.Should().BeNull();
        result.Current.DaysLeft.Should().BeNull();
        result.Current.CanTakeOrders.Should().BeTrue();
    }

    [Fact]
    public async Task Extend_ToEarlierDate_IsRejected()
    {
        var service = CreateService(out _, out var restaurant, out _, out _);

        var act = () => service.ExtendAsync(restaurant.Id, new ExtendPlanRequest(Now.AddDays(1), null), "admin@test.com");

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Cancel_StopsOrderingImmediately()
    {
        var service = CreateService(out _, out var restaurant, out _, out _);

        var result = await service.CancelAsync(restaurant.Id, new CancelPlanRequest(null), "admin@test.com");

        result.Current.Status.Should().Be(SubscriptionRules.Cancelled);
        result.Current.CanTakeOrders.Should().BeFalse();
    }

    [Fact]
    public async Task OwnerPlan_ListsOnlyActivePlans_AndHidesInternalNotes()
    {
        var service = CreateService(out _, out var restaurant, out _, out _);
        await service.GrantFreeAsync(restaurant.Id, new GrantFreePlanRequest(true, null, "internal note"), "admin@test.com");

        var plan = await service.GetOwnerPlanAsync(restaurant.Id);

        plan.AvailablePlans.Select(p => p.Name).Should().BeEquivalentTo(["Monthly", "Yearly"]);
        plan.OnlinePaymentsEnabled.Should().BeTrue();
        plan.History.Should().ContainSingle();
        typeof(OwnerPlanEventDto).GetProperty("Note").Should().BeNull();
        typeof(OwnerPlanEventDto).GetProperty("PerformedBy").Should().BeNull();
    }
}

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using QrMenu.Application.Categories;
using QrMenu.Application.Common.Exceptions;
using QrMenu.Application.Common.Interfaces;
using QrMenu.Application.Items;
using QrMenu.Application.Refunds;
using QrMenu.Application.Subscriptions;
using QrMenu.Application.Tables;
using QrMenu.Domain.Entities;
using QrMenu.Infrastructure.Auth;
using QrMenu.Infrastructure.Payments;
using QrMenu.Infrastructure.Persistence;
using QrMenu.Infrastructure.Services;
using QrMenu.Tests.Common;
using Xunit;

namespace QrMenu.Tests.Services;

public class RefundAndDuplicateTests
{
    private sealed class Clock(DateTime utc) : TimeProvider
    {
        public DateTime Now { get; set; } = utc;
        public override DateTimeOffset GetUtcNow() => new(Now, TimeSpan.Zero);
    }

    private sealed class Gateway : IPaymentGateway
    {
        public string NextStatus { get; set; } = "processed";
        /// <summary>Razorpay's charges on pay_1 (₹15), or null as if Razorpay could not be asked.</summary>
        public long? FeePaise { get; set; } = 1500;
        public int FeeLookups { get; private set; }
        public List<(string PaymentId, long Paise)> Refunds { get; } = [];
        public bool IsConfigured => true;
        public string KeyId => "rzp_test_x";
        public Task<string> CreateOrderAsync(long a, string c, string r, IDictionary<string, string> n, CancellationToken ct = default) => Task.FromResult("order_x");
        public bool IsPaymentSignatureValid(string o, string p, string s) => true;
        public bool IsWebhookSignatureValid(string b, string? s) => true;
        public Task<GatewayCheckResult> CheckCredentialsAsync(string k, string s, CancellationToken ct = default) => Task.FromResult(new GatewayCheckResult(true, "ok"));
        public Task<long?> GetPaymentFeeAsync(string paymentId, CancellationToken ct = default)
        {
            FeeLookups++;
            return Task.FromResult(FeePaise);
        }

        public Task<GatewayRefundResult> RefundAsync(string paymentId, long amountInPaise, string receipt, string? orderId, CancellationToken ct = default)
        {
            Refunds.Add((paymentId, amountInPaise));
            return Task.FromResult(new GatewayRefundResult($"rfnd_{Refunds.Count}", NextStatus));
        }
    }

    private sealed record Setup(RefundService Service, AppDbContext Db, Restaurant Restaurant, PlanPayment Payment, Gateway Gateway, Clock Clock, IServiceProvider Provider);

    private static Setup Create()
    {
        var name = Guid.NewGuid().ToString();
        var provider = new ServiceCollection().AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(name)).BuildServiceProvider();
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(name).Options);
        var clock = new Clock(new DateTime(2026, 10, 9, 10, 0, 0, DateTimeKind.Utc));

        var restaurant = new Restaurant
        {
            Id = Guid.NewGuid(), Name = "Saket Rasoi", Slug = "saket-rasoi", WhatsAppNumber = "919876543210",
            Plan = SubscriptionPlan.Paid, PlanName = "Monthly", PlanExpiresAt = clock.Now.AddMonths(1)
        };
        var plan = new PricingPlan { Id = Guid.NewGuid(), Name = "Monthly", Price = 999, DurationMonths = 1 };
        var payment = new PlanPayment
        {
            Id = Guid.NewGuid(), RestaurantId = restaurant.Id, PricingPlanId = plan.Id, PlanName = "Monthly", DurationMonths = 1,
            Amount = 999, Status = PlanPaymentStatus.Paid, GatewayOrderId = "order_1", GatewayPaymentId = "pay_1",
            CreatedAt = clock.Now.AddDays(-2), PaidAt = clock.Now.AddDays(-2)
        };
        db.Restaurants.Add(restaurant);
        db.PricingPlans.Add(plan);
        db.PlanPayments.Add(payment);
        db.SaveChanges();

        var gateway = new Gateway();
        var writer = new PaymentGatewayLogWriter(provider.GetRequiredService<IServiceScopeFactory>(), clock, NullLogger<PaymentGatewayLogWriter>.Instance);
        var service = new RefundService(db, gateway, clock, NullLogger<RefundService>.Instance, writer);
        return new Setup(service, db, restaurant, payment, gateway, clock, provider);
    }

    [Fact]
    public async Task OwnerRequest_WithinWindow_OnlyOnce_AndNotAfterSevenDays()
    {
        var s = Create();

        (await s.Service.GetOwnerRefundsAsync(s.Restaurant.Id)).Refundable.Should()
            .ContainSingle(p => p.PlanPaymentId == s.Payment.Id && p.Fee == 15 && p.RefundAmount == 984);
        var request = await s.Service.RequestAsync(s.Restaurant.Id, new RequestRefundRequest(s.Payment.Id, "Not using it"));
        request.Status.Should().Be("Requested");
        request.Amount.Should().Be(984, "Razorpay keeps its ₹15 charges");
        request.Fee.Should().Be(15);
        s.Gateway.FeeLookups.Should().Be(1, "the fee is read from Razorpay once and saved");
        (await s.Db.PlanPayments.AsNoTracking().FirstAsync(p => p.Id == s.Payment.Id)).GatewayFee.Should().Be(15);
        (await s.Service.GetOwnerRefundsAsync(s.Restaurant.Id)).Refundable.Should().BeEmpty();

        await s.Service.Invoking(x => x.RequestAsync(s.Restaurant.Id, new RequestRefundRequest(s.Payment.Id, "again")))
            .Should().ThrowAsync<ConflictException>();

        s.Clock.Now = s.Clock.Now.AddDays(8);
        await s.Service.RejectAsync(request.Id, new RejectRefundRequest("No"), "admin");
        await s.Service.Invoking(x => x.RequestAsync(s.Restaurant.Id, new RequestRefundRequest(s.Payment.Id, "late")))
            .Should().ThrowAsync<ConflictException>().WithMessage("*7 days*");
    }

    [Fact]
    public async Task FullRefund_GoesThroughRazorpay_StopsThePlan_AndCountsAsNegativeRevenue()
    {
        var s = Create();
        var request = await s.Service.RequestAsync(s.Restaurant.Id, new RequestRefundRequest(s.Payment.Id, "Changed mind"));

        var done = await s.Service.ApproveAsync(request.Id, new ApproveRefundRequest(null, "OK"), "admin@test.com");

        done.Status.Should().Be("Refunded");
        done.GatewayRefundId.Should().Be("rfnd_1");
        s.Gateway.Refunds.Should().ContainSingle(r => r.PaymentId == "pay_1" && r.Paise == 98400);
        var restaurant = await s.Db.Restaurants.AsNoTracking().FirstAsync(r => r.Id == s.Restaurant.Id);
        restaurant.PlanCancelledAt.Should().NotBeNull("everything that can go back went back, so the plan stops");
        (await s.Db.SubscriptionEvents.AsNoTracking().Where(e => e.Action == SubscriptionAction.Refunded).SumAsync(e => e.Amount)).Should().Be(984);

        await s.Service.Invoking(x => x.RefundDirectAsync(new AdminRefundRequest(s.Payment.Id, null, null, null), "admin"))
            .Should().ThrowAsync<ConflictException>().WithMessage("*refunded in full*");
    }

    [Fact]
    public async Task PartialRefund_KeepsThePlan_PendingUntilTheWebhook_AndCannotExceedWhatIsLeft()
    {
        var s = Create();
        s.Gateway.NextStatus = "pending";

        var first = await s.Service.RefundDirectAsync(new AdminRefundRequest(s.Payment.Id, null, 300, "Goodwill"), "admin");
        first.Status.Should().Be("Processing");
        (await s.Db.Restaurants.AsNoTracking().FirstAsync(r => r.Id == s.Restaurant.Id)).PlanCancelledAt.Should().BeNull();

        await s.Service.HandleGatewayUpdateAsync(first.GatewayRefundId!, "processed");
        (await s.Service.ListAsync("refunded")).Should().ContainSingle(r => r.Id == first.Id);

        await s.Service.Invoking(x => x.RefundDirectAsync(new AdminRefundRequest(s.Payment.Id, null, 700, null), "admin"))
            .Should().ThrowAsync<ConflictException>().WithMessage("*at most ₹684*");
    }

    [Fact]
    public async Task ChargedInError_SuperAdminCanGiveTheChargesBackToo()
    {
        var s = Create();

        var refund = await s.Service.RefundDirectAsync(new AdminRefundRequest(s.Payment.Id, null, null, "Charged twice", IncludeFee: true), "admin");

        refund.Amount.Should().Be(999);
        refund.Fee.Should().Be(0);
        s.Gateway.Refunds.Should().ContainSingle(r => r.Paise == 99900);
    }

    [Fact]
    public async Task WhenRazorpayCannotTellTheFee_ItIsEstimated()
    {
        var s = Create();
        s.Gateway.FeePaise = null;

        var owner = await s.Service.GetOwnerRefundsAsync(s.Restaurant.Id);

        owner.Refundable.Single().Fee.Should().Be(23.58m, "2% + 18% GST of ₹999");
        (await s.Db.PlanPayments.AsNoTracking().FirstAsync(p => p.Id == s.Payment.Id)).GatewayFee.Should().BeNull("an estimate is not saved");
    }

    [Fact]
    public async Task ManualPayment_IsRefundedByHand_AndOnlyRecorded()
    {
        var s = Create();
        var entry = new SubscriptionEvent
        {
            Id = Guid.NewGuid(), RestaurantId = s.Restaurant.Id, Action = SubscriptionAction.PaymentRecorded, Amount = 5649,
            PlanName = "Half Yearly", PaymentMethod = PaymentMethod.Upi, PerformedBy = "admin", CreatedAt = s.Clock.Now
        };
        s.Db.SubscriptionEvents.Add(entry);
        await s.Db.SaveChangesAsync();

        var refund = await s.Service.RefundDirectAsync(new AdminRefundRequest(null, entry.Id, null, "UTR 9988 sent back"), "admin");

        refund.Status.Should().Be("Refunded");
        refund.Source.Should().Be("manual");
        s.Gateway.Refunds.Should().BeEmpty();
    }

    // ---------- Duplicates ----------

    [Fact]
    public async Task CategoryNames_AreUniquePerRestaurant_IgnoringCaseAndSpaces()
    {
        var db = InMemoryDbFactory.Create();
        var service = new CategoryService(db);
        var restaurantA = Guid.NewGuid();
        var restaurantB = Guid.NewGuid();

        var starters = await service.CreateAsync(restaurantA, new CreateCategoryRequest("  Starters "));
        starters.Name.Should().Be("Starters");
        await service.Invoking(x => x.CreateAsync(restaurantA, new CreateCategoryRequest("starters"))).Should().ThrowAsync<ConflictException>();
        await service.Invoking(x => x.CreateAsync(restaurantB, new CreateCategoryRequest("Starters"))).Should().NotThrowAsync();
    }

    [Fact]
    public async Task TableNumbers_AreUnique_AlsoWhenRenaming()
    {
        var db = InMemoryDbFactory.Create();
        var service = new TableService(db);
        var restaurant = Guid.NewGuid();
        await service.CreateAsync(restaurant, new CreateTableRequest("1", 2));
        var two = await service.CreateAsync(restaurant, new CreateTableRequest("2", 2));

        await service.Invoking(x => x.CreateAsync(restaurant, new CreateTableRequest(" 1 ", 2))).Should().ThrowAsync<ConflictException>();
        await service.Invoking(x => x.UpdateAsync(restaurant, two.Id, new UpdateTableRequest("1", 2, true))).Should().ThrowAsync<ConflictException>();
        await service.Invoking(x => x.UpdateAsync(restaurant, two.Id, new UpdateTableRequest("2", 4, true))).Should().NotThrowAsync();
    }

    [Fact]
    public void SizesAndAddOns_NeedDifferentNames()
    {
        var validator = new CreateItemRequestValidator();
        var ok = new CreateItemRequest(Guid.NewGuid(), "Paneer Tikka", null, 240, null, true, null,
            [new VariantInput("Half", 150, true), new VariantInput("Full", 240, false)], [new AddOnInput("Cheese", 30)]);
        validator.Validate(ok).IsValid.Should().BeTrue();
        validator.Validate(ok with { Variants = [new VariantInput("Half", 150, true), new VariantInput(" half ", 160, false)] })
            .IsValid.Should().BeFalse();
        validator.Validate(ok with { AddOns = [new AddOnInput("Cheese", 30), new AddOnInput("CHEESE", 40)] }).IsValid.Should().BeFalse();
    }
}

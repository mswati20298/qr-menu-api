using FluentAssertions;
using Microsoft.Extensions.Options;
using QrMenu.Application.Common.Exceptions;
using QrMenu.Application.Subscriptions;
using QrMenu.Domain.Entities;
using QrMenu.Infrastructure.Pdf;
using QrMenu.Infrastructure.Services;
using QrMenu.Tests.Common;
using Xunit;

namespace QrMenu.Tests.Services;

public class OrderListAndExportTests
{
    private static Order NewOrder(Guid restaurantId, DateTime createdAt, OrderStatus status, decimal total = 100) => new()
    {
        Id = Guid.NewGuid(), RestaurantId = restaurantId, CreatedAt = createdAt, UpdatedAt = createdAt, Status = status,
        Subtotal = total, Total = total
    };

    [Fact]
    public async Task OrdersPage_GetsTheLastWeekAndOpenOrders_NotTheWholeHistory()
    {
        var db = InMemoryDbFactory.Create();
        var restaurant = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var recent = NewOrder(restaurant, now.AddDays(-2), OrderStatus.Completed);
        var oldDone = NewOrder(restaurant, now.AddDays(-40), OrderStatus.Completed);
        var oldStillOpen = NewOrder(restaurant, now.AddDays(-12), OrderStatus.Served);
        var otherRestaurant = NewOrder(Guid.NewGuid(), now, OrderStatus.Placed);
        db.Orders.AddRange(recent, oldDone, oldStillOpen, otherRestaurant);
        await db.SaveChangesAsync();
        var service = new OrderService(db, Options.Create(new SubscriptionSettings()), TestGuards.TableAccess(db));

        var page = await service.GetAllForOwnerAsync(restaurant, null);
        page.Select(o => o.Id).Should().BeEquivalentTo([recent.Id, oldStillOpen.Id]);

        var report = await service.GetAllForOwnerAsync(restaurant, null, now.AddDays(-45), now.AddDays(-30));
        report.Should().ContainSingle(o => o.Id == oldDone.Id);

        await service.Invoking(s => s.GetAllForOwnerAsync(restaurant, null, now.AddDays(-400), now))
            .Should().ThrowAsync<ConflictException>().WithMessage("*100 days*");
    }

    [Fact]
    public async Task InvoiceExport_ListsTheBillsOfThePeriod_WithGstAndHowTheyWerePaid()
    {
        var db = InMemoryDbFactory.Create();
        var restaurant = Guid.NewGuid();
        var now = DateTime.UtcNow;
        Invoice Bill(int seq, DateTime at) => new()
        {
            Id = Guid.NewGuid(), RestaurantId = restaurant, Sequence = seq, Number = $"INV-{seq}", CreatedAt = at,
            Subtotal = 200, GstPercentage = 5, GstAmount = 10, Total = 210, RestaurantName = "Saket Rasoi"
        };
        var inPeriod = Bill(1, now.AddDays(-3));
        var before = Bill(2, now.AddDays(-50));
        db.Invoices.AddRange(inPeriod, before);
        var order = NewOrder(restaurant, inPeriod.CreatedAt, OrderStatus.Completed, 210);
        order.InvoiceId = inPeriod.Id;
        order.PaymentStatus = OrderPaymentStatus.Paid;
        order.PaymentMethod = PaymentMethod.Upi;
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var rows = await new InvoiceService(db, new InvoicePdfService(), TimeProvider.System).ExportAsync(restaurant, now.AddDays(-30), now);

        var row = rows.Should().ContainSingle().Subject;
        row.Number.Should().Be("INV-1");
        row.GstAmount.Should().Be(10);
        row.PaymentStatus.Should().Be("Paid");
        row.PaymentMethods.Should().Be("Upi");
    }
}

using FluentAssertions;
using Microsoft.Extensions.Options;
using QrMenu.Application.Common.Exceptions;
using QrMenu.Application.Invoices;
using QrMenu.Application.Kitchen;
using QrMenu.Application.Orders;
using QrMenu.Application.Subscriptions;
using QrMenu.Domain.Entities;
using QrMenu.Infrastructure.Auth;
using QrMenu.Infrastructure.Pdf;
using QrMenu.Infrastructure.Persistence;
using QrMenu.Infrastructure.Services;
using QrMenu.Tests.Common;
using Xunit;

namespace QrMenu.Tests.Services;

public class InvoiceAndKitchenTests
{
    private static Restaurant AddRestaurant(AppDbContext db, string slug = "saket-rasoi")
    {
        var restaurant = new Restaurant
        {
            Id = Guid.NewGuid(),
            Name = "Saket Rasoi",
            Slug = slug,
            WhatsAppNumber = "919876543210",
            Address = "12, Saket, New Delhi",
            GstNumber = "07ABCDE1234F1Z5",
            InvoicePrefix = "SR",
            IsGstEnabled = true,
            GstPercentage = 5,
            UpiId = "saket@okicici",
            UpiPayeeName = "Saket Rasoi"
        };
        db.Restaurants.Add(restaurant);
        return restaurant;
    }

    private static Order AddOrder(AppDbContext db, Restaurant restaurant, string? table, decimal subtotal,
        OrderStatus status = OrderStatus.Served, DateTime? createdAt = null)
    {
        var gst = Math.Round(subtotal * 0.05m, 2);
        var order = new Order
        {
            Id = Guid.NewGuid(),
            RestaurantId = restaurant.Id,
            TableNumberSnapshot = table,
            CustomerName = "Asha",
            Status = status,
            Subtotal = subtotal,
            GstAmount = gst,
            Total = subtotal + gst,
            CreatedAt = createdAt ?? DateTime.UtcNow.AddMinutes(-30),
            Items =
            [
                new OrderItem { Id = Guid.NewGuid(), ItemName = "Paneer Tikka", Qty = 1, UnitPrice = subtotal, LineTotal = subtotal }
            ]
        };
        db.Orders.Add(order);
        return order;
    }

    private static InvoiceService CreateInvoiceService(AppDbContext db) =>
        new(db, new InvoicePdfService(), TimeProvider.System);

    [Fact]
    public async Task TableInvoice_BillsOnlyOpenOrdersOfThatTable_AndMergesSameItems()
    {
        var db = InMemoryDbFactory.Create();
        var restaurant = AddRestaurant(db);
        AddOrder(db, restaurant, "5", 200);
        AddOrder(db, restaurant, "5", 200);
        AddOrder(db, restaurant, "5", 100, OrderStatus.Cancelled);
        AddOrder(db, restaurant, "5", 100, createdAt: DateTime.UtcNow.AddDays(-2));
        AddOrder(db, restaurant, "6", 300);
        await db.SaveChangesAsync();

        var invoice = await CreateInvoiceService(db).CreateAsync(restaurant.Id, new CreateInvoiceRequest(null, "5"));

        invoice.Number.Should().Be("SR-0001");
        invoice.OrderIds.Should().HaveCount(2);
        invoice.Subtotal.Should().Be(400);
        invoice.GstAmount.Should().Be(20);
        invoice.Total.Should().Be(420);
        invoice.Lines.Should().ContainSingle(l => l.Name == "Paneer Tikka" && l.Qty == 2 && l.Amount == 400);
        invoice.PaymentStatus.Should().Be("Unpaid");
    }

    [Fact]
    public async Task Invoices_AreNumberedPerRestaurant_AndAnOrderIsBilledOnce()
    {
        var db = InMemoryDbFactory.Create();
        var a = AddRestaurant(db, "a");
        var b = AddRestaurant(db, "b");
        var a1 = AddOrder(db, a, "1", 100);
        var a2 = AddOrder(db, a, "2", 100);
        var b1 = AddOrder(db, b, "1", 100);
        await db.SaveChangesAsync();
        var service = CreateInvoiceService(db);

        var first = await service.CreateAsync(a.Id, new CreateInvoiceRequest(a1.Id, null));
        var again = await service.CreateAsync(a.Id, new CreateInvoiceRequest(a1.Id, null));
        var second = await service.CreateAsync(a.Id, new CreateInvoiceRequest(a2.Id, null));
        var otherRestaurant = await service.CreateAsync(b.Id, new CreateInvoiceRequest(b1.Id, null));

        again.Id.Should().Be(first.Id);
        first.Number.Should().Be("SR-0001");
        second.Number.Should().Be("SR-0002");
        otherRestaurant.Number.Should().Be("SR-0001");
    }

    [Fact]
    public async Task Invoice_FromAnotherRestaurant_IsNotFound()
    {
        var db = InMemoryDbFactory.Create();
        var a = AddRestaurant(db, "a");
        var b = AddRestaurant(db, "b");
        var order = AddOrder(db, a, "1", 100);
        await db.SaveChangesAsync();
        var service = CreateInvoiceService(db);
        var invoice = await service.CreateAsync(a.Id, new CreateInvoiceRequest(order.Id, null));

        var bill = () => service.CreateAsync(b.Id, new CreateInvoiceRequest(order.Id, null));
        var read = () => service.GetAsync(b.Id, invoice.Id);

        await bill.Should().ThrowAsync<NotFoundException>();
        await read.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task MarkPaid_PaysEveryOrder_AndPdfIsGenerated()
    {
        var db = InMemoryDbFactory.Create();
        var restaurant = AddRestaurant(db);
        AddOrder(db, restaurant, "5", 200);
        AddOrder(db, restaurant, "5", 150);
        await db.SaveChangesAsync();
        var service = CreateInvoiceService(db);
        var invoice = await service.CreateAsync(restaurant.Id, new CreateInvoiceRequest(null, "5"));

        var paid = await service.MarkPaidAsync(restaurant.Id, invoice.Id, new MarkInvoicePaidRequest("Cash", null));
        var (receipt, _) = await service.GetPdfAsync(restaurant.Id, invoice.Id, receipt: true);
        var (a4, _) = await service.GetPdfAsync(restaurant.Id, invoice.Id, receipt: false);

        paid.PaymentStatus.Should().Be("Paid");
        db.Orders.Should().OnlyContain(o => o.PaymentStatus == OrderPaymentStatus.Paid && o.PaymentMethod == PaymentMethod.Cash);
        receipt.Take(4).Should().Equal("%PDF"u8.ToArray());
        a4.Take(4).Should().Equal("%PDF"u8.ToArray());
    }

    [Fact]
    public async Task PaymentClaim_ThenStaffConfirm()
    {
        var db = InMemoryDbFactory.Create();
        var restaurant = AddRestaurant(db);
        var order = AddOrder(db, restaurant, "5", 200);
        await db.SaveChangesAsync();
        var service = new OrderService(db, Options.Create(new SubscriptionSettings()), TestGuards.TableAccess(db));

        var claimed = await service.ClaimPublicPaymentAsync(restaurant.Slug, order.Id, new ClaimPaymentRequest("412345678901"));
        var confirmed = await service.UpdatePaymentAsync(restaurant.Id, order.Id, new UpdatePaymentRequest("Paid", "Upi", null));

        claimed.PaymentStatus.Should().Be("Claimed");
        claimed.PaymentReference.Should().Be("412345678901");
        confirmed.PaymentStatus.Should().Be("Paid");
        confirmed.PaymentMethod.Should().Be("Upi");
    }

    [Fact]
    public async Task PaymentClaim_WithoutUpiSetUp_IsRejected()
    {
        var db = InMemoryDbFactory.Create();
        var restaurant = AddRestaurant(db);
        restaurant.UpiId = null;
        var order = AddOrder(db, restaurant, "5", 200);
        await db.SaveChangesAsync();
        var service = new OrderService(db, Options.Create(new SubscriptionSettings()), TestGuards.TableAccess(db));

        var act = () => service.ClaimPublicPaymentAsync(restaurant.Slug, order.Id, new ClaimPaymentRequest(null));

        await act.Should().ThrowAsync<ConflictException>();
    }

    private static KitchenService CreateKitchenService(AppDbContext db) => new(
        db,
        new BcryptPasswordHasher(),
        new JwtTokenService(Options.Create(new JwtSettings
        {
            Secret = "test-secret-key-that-is-at-least-32-characters-long",
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            ExpiryMinutes = 60
        })),
        TimeProvider.System);

    [Fact]
    public async Task Kitchen_AdvancesAndRevertsOrders_AndShowsOnlyItsBoard()
    {
        var db = InMemoryDbFactory.Create();
        var restaurant = AddRestaurant(db);
        var other = AddRestaurant(db, "other");
        var order = AddOrder(db, restaurant, "5", 200, OrderStatus.Placed);
        AddOrder(db, restaurant, "6", 200, OrderStatus.Completed);
        AddOrder(db, other, "1", 200, OrderStatus.Placed);
        await db.SaveChangesAsync();
        var kitchen = CreateKitchenService(db);

        var board = await kitchen.GetBoardAsync(restaurant.Id);
        var preparing = await kitchen.AdvanceAsync(restaurant.Id, order.Id);
        var served = await kitchen.AdvanceAsync(restaurant.Id, order.Id);
        var tooFar = () => kitchen.AdvanceAsync(restaurant.Id, order.Id);
        await tooFar.Should().ThrowAsync<ConflictException>();
        var undone = await kitchen.RevertAsync(restaurant.Id, order.Id);
        var otherRestaurant = () => kitchen.AdvanceAsync(other.Id, order.Id);

        board.Orders.Should().ContainSingle(o => o.Id == order.Id);
        preparing.Status.Should().Be("Preparing");
        served.Status.Should().Be("Served");
        undone.Status.Should().Be("Preparing");
        await otherRestaurant.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task KitchenBoard_ShowsOldOpenOrders_ButHidesOldServedOnes()
    {
        var db = InMemoryDbFactory.Create();
        var restaurant = AddRestaurant(db);
        var oldNew = AddOrder(db, restaurant, "2", 100, OrderStatus.Placed, DateTime.UtcNow.AddHours(-34));
        var oldPreparing = AddOrder(db, restaurant, "3", 100, OrderStatus.Preparing, DateTime.UtcNow.AddHours(-89));
        var oldServed = AddOrder(db, restaurant, "5", 100, OrderStatus.Served, DateTime.UtcNow.AddHours(-161));
        oldServed.UpdatedAt = DateTime.UtcNow.AddHours(-161);
        await db.SaveChangesAsync();

        var board = await CreateKitchenService(db).GetBoardAsync(restaurant.Id);

        board.Orders.Select(o => o.Id).Should().BeEquivalentTo([oldNew.Id, oldPreparing.Id]);
    }

    [Fact]
    public async Task KitchenLogin_NeedsTheRightPin()
    {
        var db = InMemoryDbFactory.Create();
        var restaurant = AddRestaurant(db, $"pin-{Guid.NewGuid():N}");
        restaurant.KitchenPinHash = new BcryptPasswordHasher().Hash("4821");
        await db.SaveChangesAsync();
        var kitchen = CreateKitchenService(db);

        var wrong = () => kitchen.LoginAsync(new KitchenLoginRequest(restaurant.Slug, "1111"));
        await wrong.Should().ThrowAsync<UnauthorizedAppException>();

        var ok = await kitchen.LoginAsync(new KitchenLoginRequest(restaurant.Slug, "4821"));
        ok.Token.Should().NotBeNullOrEmpty();
        ok.RestaurantName.Should().Be("Saket Rasoi");
    }
}

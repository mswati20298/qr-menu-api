using FluentAssertions;
using Microsoft.Extensions.Options;
using QrMenu.Application.Common.Exceptions;
using QrMenu.Application.Invoices;
using QrMenu.Application.Orders;
using QrMenu.Application.Subscriptions;
using QrMenu.Domain.Entities;
using QrMenu.Infrastructure.Pdf;
using QrMenu.Infrastructure.Persistence;
using QrMenu.Infrastructure.Services;
using QrMenu.Tests.Common;
using Xunit;

namespace QrMenu.Tests.Services;

/// <summary>Orders and bills entered by staff from the admin panel.</summary>
public class StaffOrderTests
{
    private sealed record Menu(Restaurant Restaurant, MenuItem PaneerTikka, ItemVariant Half, ItemVariant Full, ItemAddOn ExtraCheese, MenuItem Chaas);

    private static Menu Seed(AppDbContext db, DateTime? planEnds = null)
    {
        var restaurant = new Restaurant
        {
            Id = Guid.NewGuid(), Name = "Saket Rasoi", Slug = "saket-rasoi", WhatsAppNumber = "919876543210",
            IsGstEnabled = true, GstPercentage = 5, IsServiceChargeEnabled = true, ServiceChargePercentage = 10,
            Plan = SubscriptionPlan.Paid, PlanExpiresAt = planEnds ?? DateTime.UtcNow.AddDays(30)
        };
        var category = new Category { Id = Guid.NewGuid(), RestaurantId = restaurant.Id, Name = "Starters" };
        var half = new ItemVariant { Id = Guid.NewGuid(), Name = "Half", Price = 250, IsDefault = true };
        var full = new ItemVariant { Id = Guid.NewGuid(), Name = "Full", Price = 350 };
        var cheese = new ItemAddOn { Id = Guid.NewGuid(), Name = "Extra cheese", Price = 40 };
        var paneer = new MenuItem
        {
            Id = Guid.NewGuid(), CategoryId = category.Id, Name = "Paneer Tikka", Price = 250, IsAvailable = true,
            Variants = [half, full], AddOns = [cheese]
        };
        var chaas = new MenuItem { Id = Guid.NewGuid(), CategoryId = category.Id, Name = "Masala Chaas", Price = 60, IsAvailable = true };
        category.MenuItems = [paneer, chaas];
        db.Restaurants.Add(restaurant);
        db.Categories.Add(category);
        db.SaveChanges();
        return new Menu(restaurant, paneer, half, full, cheese, chaas);
    }

    private static OrderService Orders(AppDbContext db) => new(db, Options.Create(new SubscriptionSettings()), TestGuards.TableAccess(db));

    [Fact]
    public async Task StaffOrder_IsPricedFromTheMenu_WithSizesAddOnsAndCharges()
    {
        var db = InMemoryDbFactory.Create();
        var m = Seed(db);

        var order = await Orders(db).CreateStaffOrderAsync(m.Restaurant.Id, new StaffOrderRequest(
            null, "Walk-in", null, null, SkipServiceCharge: false, SendToKitchen: true,
            [new OrderItemInput(m.PaneerTikka.Id, m.Full.Id, [m.ExtraCheese.Id], 2), new OrderItemInput(m.Chaas.Id, null, null, 1)]));

        // (350 + 40) × 2 + 60 = 840; service 10% = 84; GST 5% of 924 = 46.20
        order.Subtotal.Should().Be(840);
        order.ServiceChargeAmount.Should().Be(84);
        order.GstAmount.Should().Be(46.20m);
        order.Total.Should().Be(970.20m);
        order.Status.Should().Be("Placed");
        order.Source.Should().Be("Staff");
        order.TableNumber.Should().BeNull();
    }

    [Fact]
    public async Task HalfPaise_RoundUp_LikeTheBillPreview()
    {
        var db = InMemoryDbFactory.Create();
        var m = Seed(db);
        m.Restaurant.ServiceChargePercentage = 5;
        await db.SaveChangesAsync();
        var kebabPrice = 190m;
        var kebab = new MenuItem { Id = Guid.NewGuid(), CategoryId = m.Chaas.CategoryId, Name = "Hara Bhara Kebab", Price = kebabPrice, IsAvailable = true };
        db.MenuItems.Add(kebab);
        await db.SaveChangesAsync();

        // 2 × 190 + 350 = 730; service 5% = 36.50; GST 5% of 766.50 = 38.325 → 38.33 (not banker's 38.32)
        var order = await Orders(db).CreateStaffOrderAsync(m.Restaurant.Id, new StaffOrderRequest(
            null, null, null, null, false, true,
            [new OrderItemInput(kebab.Id, null, null, 2), new OrderItemInput(m.PaneerTikka.Id, m.Full.Id, null, 1)]));

        order.GstAmount.Should().Be(38.33m);
        order.Total.Should().Be(804.83m);
    }

    [Fact]
    public async Task StaffOrder_NotForKitchen_IsRecordedAsServed()
    {
        var db = InMemoryDbFactory.Create();
        var m = Seed(db);

        var order = await Orders(db).CreateStaffOrderAsync(m.Restaurant.Id, new StaffOrderRequest(
            "4", null, null, null, SkipServiceCharge: true, SendToKitchen: false, [new OrderItemInput(m.Chaas.Id, null, null, 3)]));

        order.Status.Should().Be("Served");
        order.TableNumber.Should().Be("4");
        order.ServiceChargeAmount.Should().Be(0);
    }

    [Fact]
    public async Task StaffOrder_SizedDishWithoutSize_IsRejected()
    {
        var db = InMemoryDbFactory.Create();
        var m = Seed(db);

        var act = () => Orders(db).CreateStaffOrderAsync(m.Restaurant.Id, new StaffOrderRequest(
            null, null, null, null, false, true, [new OrderItemInput(m.PaneerTikka.Id, null, null, 1)]));

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task StaffOrder_WithAnotherRestaurantsDish_IsRejected()
    {
        var db = InMemoryDbFactory.Create();
        var mine = Seed(db);
        var other = Seed(db);

        var act = () => Orders(db).CreateStaffOrderAsync(mine.Restaurant.Id, new StaffOrderRequest(
            null, null, null, null, false, true, [new OrderItemInput(other.Chaas.Id, null, null, 1)]));

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task StaffOrder_AfterPlanEnded_IsRefused()
    {
        var db = InMemoryDbFactory.Create();
        var m = Seed(db, planEnds: DateTime.UtcNow.AddDays(-30));

        var act = () => Orders(db).CreateStaffOrderAsync(m.Restaurant.Id, new StaffOrderRequest(
            null, null, null, null, false, true, [new OrderItemInput(m.Chaas.Id, null, null, 1)]));

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task StaffOrderAddedToTable_IsIncludedInTheTableBill()
    {
        var db = InMemoryDbFactory.Create();
        var m = Seed(db);
        var orders = Orders(db);
        await orders.CreateStaffOrderAsync(m.Restaurant.Id, new StaffOrderRequest(
            "5", null, null, null, true, true, [new OrderItemInput(m.Chaas.Id, null, null, 2)]));
        await orders.CreateStaffOrderAsync(m.Restaurant.Id, new StaffOrderRequest(
            "5", null, null, null, true, false, [new OrderItemInput(m.PaneerTikka.Id, m.Half.Id, null, 1)]));

        var invoice = await new InvoiceService(db, new InvoicePdfService(), TimeProvider.System)
            .CreateAsync(m.Restaurant.Id, new CreateInvoiceRequest(null, "5"));

        invoice.OrderIds.Should().HaveCount(2);
        invoice.Subtotal.Should().Be(370); // 2 × 60 + 250
    }
}

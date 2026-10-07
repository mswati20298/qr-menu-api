using FluentAssertions;
using Microsoft.Extensions.Options;
using QrMenu.Application.Common.Exceptions;
using QrMenu.Application.Orders;
using QrMenu.Application.PublicMenu;
using QrMenu.Application.ServiceRequests;
using QrMenu.Application.Subscriptions;
using QrMenu.Application.Tables;
using QrMenu.Domain.Entities;
using QrMenu.Infrastructure.Persistence;
using QrMenu.Infrastructure.Services;
using QrMenu.Tests.Common;
using Xunit;

namespace QrMenu.Tests.Services;

/// <summary>Only guests who scanned a table's QR can order for it (when the restaurant turns that on).</summary>
public class TableQrAccessTests
{
    private sealed class Clock(DateTime utc) : TimeProvider
    {
        public DateTime Now { get; set; } = utc;
        public override DateTimeOffset GetUtcNow() => new(Now, TimeSpan.Zero);
    }

    private sealed record Setup(AppDbContext Db, Restaurant Restaurant, Table Table5, MenuItem Item, Clock Clock);

    private static Setup Seed(bool requireTableQr = true, bool allowTakeaway = false)
    {
        var db = InMemoryDbFactory.Create();
        var restaurant = new Restaurant
        {
            Id = Guid.NewGuid(), Name = "Saket Rasoi", Slug = "saket-rasoi", WhatsAppNumber = "919876543210",
            Plan = SubscriptionPlan.Paid, PlanExpiresAt = DateTime.UtcNow.AddDays(30),
            RequireTableQr = requireTableQr, AllowLinkTakeaway = allowTakeaway, QrSessionHours = 3
        };
        var table5 = new Table { Id = Guid.NewGuid(), RestaurantId = restaurant.Id, Number = "5", IsActive = true, QrCode = TableCodes.New() };
        var table8 = new Table { Id = Guid.NewGuid(), RestaurantId = restaurant.Id, Number = "8", IsActive = true, QrCode = TableCodes.New() };
        var category = new Category { Id = Guid.NewGuid(), RestaurantId = restaurant.Id, Name = "Starters" };
        var item = new MenuItem { Id = Guid.NewGuid(), CategoryId = category.Id, Name = "Paneer Tikka", Price = 240, IsAvailable = true };
        category.MenuItems = [item];
        db.Restaurants.Add(restaurant);
        db.Tables.AddRange(table5, table8);
        db.Categories.Add(category);
        db.SaveChanges();
        return new Setup(db, restaurant, table5, item, new Clock(DateTime.UtcNow));
    }

    private static PublicMenuService Menu(Setup s) => new(s.Db, Options.Create(new SubscriptionSettings()), s.Clock, TestGuards.Tokens());
    private static OrderService Orders(Setup s) => new(s.Db, Options.Create(new SubscriptionSettings()), TestGuards.TableAccess(s.Db, s.Clock));
    private static ServiceRequestService Requests(Setup s) => new(s.Db, TestGuards.TableAccess(s.Db, s.Clock));

    private static CreateOrderRequest Order(Setup s, string? table, string? session) =>
        new(table, "Guest", "9876543210", null, false, [new OrderItemInput(s.Item.Id, null, null, 1)], session);

    [Fact]
    public void Codes_AreTwelveUnambiguousCharacters_AndMatchIgnoringCase()
    {
        var code = TableCodes.New();

        code.Should().HaveLength(12).And.MatchRegex("^[A-HJKMNP-Z2-9]+$");
        TableCodes.Matches(code, code.ToLowerInvariant()).Should().BeTrue();
        TableCodes.Matches(code, "AAAAAAAAAAAA").Should().BeFalse();
        TableCodes.Matches(code, null).Should().BeFalse();
    }

    [Fact]
    public async Task ScanningWithTheRightCode_StartsASession_AndTheOrderGoesToThatTable()
    {
        var s = Seed();
        var session = await Menu(s).StartTableSessionAsync("saket-rasoi", new StartTableSessionRequest("5", s.Table5.QrCode));

        session.TableNumber.Should().Be("5");
        session.ExpiresAt.Should().BeCloseTo(s.Clock.Now.AddHours(3), TimeSpan.FromSeconds(5));

        // The table always comes from the session, whatever number the request claims.
        var order = await Orders(s).CreatePublicOrderAsync("saket-rasoi", Order(s, "8", session.Token));
        order.TableNumber.Should().Be("5");
    }

    [Fact]
    public async Task WrongCode_DoesNotStartASession()
    {
        var s = Seed();

        var act = () => Menu(s).StartTableSessionAsync("saket-rasoi", new StartTableSessionRequest("5", "WRONGCODE234"));

        (await act.Should().ThrowAsync<ForbiddenException>()).Which.Code.Should().Be("table_qr_invalid");
    }

    [Fact]
    public async Task OnlyTableQrOrders_RefusesALinkWithJustATableNumber()
    {
        var s = Seed();

        var act = () => Orders(s).CreatePublicOrderAsync("saket-rasoi", Order(s, "5", null));

        (await act.Should().ThrowAsync<ForbiddenException>()).Which.Code.Should().Be(TableAccessGuard.QrRequiredCode);
    }

    [Fact]
    public async Task SessionExpires_AfterTheSetHours()
    {
        var s = Seed();
        var session = await Menu(s).StartTableSessionAsync("saket-rasoi", new StartTableSessionRequest("5", s.Table5.QrCode));

        s.Clock.Now = s.Clock.Now.AddHours(3).AddMinutes(1);
        var act = () => Orders(s).CreatePublicOrderAsync("saket-rasoi", Order(s, null, session.Token));

        (await act.Should().ThrowAsync<ForbiddenException>()).Which.Code.Should().Be(TableAccessGuard.SessionExpiredCode);
    }

    [Fact]
    public async Task ResettingTheCode_EndsSessionsStartedWithTheOldOne()
    {
        var s = Seed();
        var session = await Menu(s).StartTableSessionAsync("saket-rasoi", new StartTableSessionRequest("5", s.Table5.QrCode));

        var reset = await new TableService(s.Db).ResetQrCodeAsync(s.Restaurant.Id, s.Table5.Id);
        var act = () => Orders(s).CreatePublicOrderAsync("saket-rasoi", Order(s, null, session.Token));

        reset.QrCode.Should().NotBe(session.Token);
        (await act.Should().ThrowAsync<ForbiddenException>()).Which.Code.Should().Be(TableAccessGuard.SessionExpiredCode);
    }

    [Fact]
    public async Task TakeawayFromTheLink_FollowsTheSetting()
    {
        var off = Seed(allowTakeaway: false);
        var refused = () => Orders(off).CreatePublicOrderAsync("saket-rasoi", Order(off, null, null));
        (await refused.Should().ThrowAsync<ForbiddenException>()).Which.Code.Should().Be(TableAccessGuard.TakeawayOffCode);

        var on = Seed(allowTakeaway: true);
        var order = await Orders(on).CreatePublicOrderAsync("saket-rasoi", Order(on, null, null));
        order.TableNumber.Should().BeNull();
    }

    [Fact]
    public async Task SettingOff_KeepsOldPrintedQrCodesWorking()
    {
        var s = Seed(requireTableQr: false, allowTakeaway: true);

        var order = await Orders(s).CreatePublicOrderAsync("saket-rasoi", Order(s, "5", null));

        order.TableNumber.Should().Be("5");
    }

    [Fact]
    public async Task WaterBillWaiterRequests_NeedTheTableQrToo()
    {
        var s = Seed();
        var refused = () => Requests(s).CreatePublicAsync("saket-rasoi", new CreateServiceRequest("5", "Water"));
        await refused.Should().ThrowAsync<ForbiddenException>();

        var session = await Menu(s).StartTableSessionAsync("saket-rasoi", new StartTableSessionRequest("5", s.Table5.QrCode));
        var request = await Requests(s).CreatePublicAsync("saket-rasoi", new CreateServiceRequest(null, "Water", session.Token));
        request.TableNumber.Should().Be("5");
    }
}

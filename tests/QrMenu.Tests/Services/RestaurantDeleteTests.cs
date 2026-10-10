using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QrMenu.Application.Common.Exceptions;
using QrMenu.Application.Subscriptions;
using QrMenu.Domain.Entities;
using QrMenu.Infrastructure.Auth;
using QrMenu.Infrastructure.Services;
using QrMenu.Tests.Common;
using Xunit;

namespace QrMenu.Tests.Services;

public class RestaurantDeleteTests
{
    private static (SuperAdminService Service, QrMenu.Infrastructure.Persistence.AppDbContext Db, Restaurant Restaurant) Create()
    {
        var db = InMemoryDbFactory.Create();
        var restaurant = new Restaurant
        {
            Id = Guid.NewGuid(), Name = "Test Dhaba", Slug = "test-dhaba", WhatsAppNumber = "919876543210", IsActive = true
        };
        db.Restaurants.Add(restaurant);
        db.SaveChanges();
        var service = new SuperAdminService(db, new BcryptPasswordHasher(), null!, Options.Create(new SubscriptionSettings()), null!);
        return (service, db, restaurant);
    }

    [Fact]
    public async Task SoftDelete_HidesTheRestaurant_AndRestoreBringsItBack()
    {
        var (service, db, restaurant) = Create();

        var deleted = await service.SoftDeleteRestaurantAsync(restaurant.Id, "admin@test.com");

        deleted.DeletedAt.Should().NotBeNull();
        deleted.IsActive.Should().BeFalse("its owner is signed out and the menu goes offline");
        (await service.ListRestaurantsAsync(null, null, null, 1, 20)).Items.Should().BeEmpty("deleted ones are not in the normal list");
        (await service.ListRestaurantsAsync(null, "deleted", null, 1, 20)).Items.Should().ContainSingle();
        await service.Invoking(s => s.SetRestaurantStatusAsync(restaurant.Id, true)).Should().ThrowAsync<ConflictException>();

        var restored = await service.RestoreRestaurantAsync(restaurant.Id);

        restored.DeletedAt.Should().BeNull();
        restored.IsActive.Should().BeTrue();
        (await service.ListRestaurantsAsync(null, null, null, 1, 20)).Items.Should().ContainSingle();
    }

    [Fact]
    public async Task HardDelete_OnlyAfterSoftDelete_AndOnlyWithTheNameTyped()
    {
        var (service, db, restaurant) = Create();

        await service.Invoking(s => s.HardDeleteRestaurantAsync(restaurant.Id, "Test Dhaba"))
            .Should().ThrowAsync<ConflictException>().WithMessage("*soft delete*");

        await service.SoftDeleteRestaurantAsync(restaurant.Id, "admin");
        await service.Invoking(s => s.HardDeleteRestaurantAsync(restaurant.Id, "Test"))
            .Should().ThrowAsync<ConflictException>().WithMessage("*does not match*");

        await service.HardDeleteRestaurantAsync(restaurant.Id, "  test dhaba ");

        (await db.Restaurants.AnyAsync(r => r.Id == restaurant.Id)).Should().BeFalse();
    }
}

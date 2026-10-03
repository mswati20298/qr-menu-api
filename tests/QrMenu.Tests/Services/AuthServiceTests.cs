using FluentAssertions;
using Microsoft.Extensions.Options;
using QrMenu.Application.Auth;
using QrMenu.Application.Common.Exceptions;
using QrMenu.Application.Subscriptions;
using QrMenu.Infrastructure.Auth;
using QrMenu.Infrastructure.Services;
using QrMenu.Tests.Common;
using Xunit;

namespace QrMenu.Tests.Services;

public class AuthServiceTests
{
    private static AuthService CreateService(out QrMenu.Infrastructure.Persistence.AppDbContext db)
    {
        db = InMemoryDbFactory.Create();
        var jwtSettings = Options.Create(new JwtSettings
        {
            Secret = "test-secret-key-that-is-at-least-32-characters-long",
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            ExpiryMinutes = 60
        });
        return new AuthService(db, new BcryptPasswordHasher(), new JwtTokenService(jwtSettings), Options.Create(new SubscriptionSettings()));
    }

    [Fact]
    public async Task RegisterAsync_CreatesRestaurantAndOwner()
    {
        var service = CreateService(out var db);

        var result = await service.RegisterAsync(new RegisterRequest(
            "Saket Rasoi", "Ramesh Gupta", "owner@test.com", "Password1", "919876543210"));

        result.Token.Should().NotBeNullOrEmpty();
        result.RestaurantSlug.Should().Be("saket-rasoi");
        db.Restaurants.Should().ContainSingle();
        db.Users.Should().ContainSingle();
    }

    [Fact]
    public async Task RegisterAsync_StartsThreeDayTrial()
    {
        var service = CreateService(out var db);

        await service.RegisterAsync(new RegisterRequest(
            "Saket Rasoi", "Ramesh Gupta", "owner@test.com", "Password1", "919876543210"));

        var restaurant = db.Restaurants.Single();
        restaurant.Plan.Should().Be(QrMenu.Domain.Entities.SubscriptionPlan.Trial);
        restaurant.PlanExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddDays(3), TimeSpan.FromMinutes(1));
        db.SubscriptionEvents.Should().ContainSingle(e => e.Action == QrMenu.Domain.Entities.SubscriptionAction.TrialStarted);
    }

    [Fact]
    public async Task RegisterAsync_GeneratesUniqueSlug_WhenNameCollides()
    {
        var service = CreateService(out _);

        await service.RegisterAsync(new RegisterRequest(
            "Saket Rasoi", "Owner One", "owner1@test.com", "Password1", "919876543210"));

        var second = await service.RegisterAsync(new RegisterRequest(
            "Saket Rasoi", "Owner Two", "owner2@test.com", "Password1", "919876543211"));

        second.RestaurantSlug.Should().Be("saket-rasoi-2");
    }

    [Fact]
    public async Task RegisterAsync_ThrowsConflict_WhenEmailAlreadyExists()
    {
        var service = CreateService(out _);

        await service.RegisterAsync(new RegisterRequest(
            "Saket Rasoi", "Owner One", "owner@test.com", "Password1", "919876543210"));

        var act = async () => await service.RegisterAsync(new RegisterRequest(
            "Another Place", "Owner Two", "owner@test.com", "Password1", "919876543211"));

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task LoginAsync_ThrowsUnauthorized_WhenPasswordIsWrong()
    {
        var service = CreateService(out _);

        await service.RegisterAsync(new RegisterRequest(
            "Saket Rasoi", "Owner One", "owner@test.com", "Password1", "919876543210"));

        var act = async () => await service.LoginAsync(new LoginRequest("owner@test.com", "WrongPassword"));

        await act.Should().ThrowAsync<UnauthorizedAppException>();
    }

    [Fact]
    public async Task LoginAsync_ReturnsToken_WhenCredentialsAreCorrect()
    {
        var service = CreateService(out _);

        await service.RegisterAsync(new RegisterRequest(
            "Saket Rasoi", "Owner One", "owner@test.com", "Password1", "919876543210"));

        var result = await service.LoginAsync(new LoginRequest("owner@test.com", "Password1"));

        result.Token.Should().NotBeNullOrEmpty();
        result.RestaurantSlug.Should().Be("saket-rasoi");
    }
}

using FluentAssertions;
using Microsoft.Extensions.Options;
using QrMenu.Application.Auth;
using QrMenu.Application.Common;
using QrMenu.Application.Common.Exceptions;
using QrMenu.Application.Subscriptions;
using QrMenu.Infrastructure.Auth;
using QrMenu.Infrastructure.Services;
using QrMenu.Tests.Common;
using Xunit;

namespace QrMenu.Tests.Services;

public class AuthServiceTests
{
    private static AuthService CreateService(out QrMenu.Infrastructure.Persistence.AppDbContext db, string rootDomain = "")
    {
        db = InMemoryDbFactory.Create();
        var jwtSettings = Options.Create(new JwtSettings
        {
            Secret = "test-secret-key-that-is-at-least-32-characters-long",
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            ExpiryMinutes = 60
        });
        return new AuthService(db, new BcryptPasswordHasher(), new JwtTokenService(jwtSettings),
            new PlatformSettingsService(db, Options.Create(new SubscriptionSettings())),
            Options.Create(new SiteSettings { RootDomain = rootDomain }));
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

    [Theory]
    [InlineData(2)]
    [InlineData(0)]
    public async Task RegisterAsync_UsesTrialDaysSetBySuperAdmin(int trialDays)
    {
        var service = CreateService(out var db);
        await new PlatformSettingsService(db, Options.Create(new SubscriptionSettings()))
            .UpdateAsync(new QrMenu.Application.Platform.UpdatePlatformSettingsRequest(trialDays), "admin@test.com");

        await service.RegisterAsync(new RegisterRequest(
            "Saket Rasoi", "Ramesh Gupta", "owner@test.com", "Password1", "919876543210"));

        var restaurant = db.Restaurants.Single();
        restaurant.Plan.Should().Be(QrMenu.Domain.Entities.SubscriptionPlan.Trial);
        restaurant.PlanExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddDays(trialDays), TimeSpan.FromMinutes(1));

        // 0 days = no trial: customers cannot order until a plan is bought.
        var canOrder = SubscriptionRules.CanTakeOrders(restaurant, graceDays: 7, DateTime.UtcNow.AddSeconds(1));
        canOrder.Should().Be(trialDays > 0);
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

    [Fact]
    public async Task RegisterAsync_GivesSlugAsSubdomain_WhenSubdomainsEnabled()
    {
        var service = CreateService(out var db, "qrenvo.com");

        await service.RegisterAsync(new RegisterRequest("Saket Rasoi", "A", "a@test.com", "Password1", "919876543210"));
        await service.RegisterAsync(new RegisterRequest("Admin", "B", "b@test.com", "Password1", "919876543210"));

        db.Restaurants.Single(r => r.Slug == "saket-rasoi").Subdomain.Should().Be("saket-rasoi");
        // "admin" is reserved, so that restaurant only gets the /m/{slug} link.
        db.Restaurants.Single(r => r.Slug == "admin").Subdomain.Should().BeNull();
    }

    [Fact]
    public async Task RegisterAsync_NoSubdomain_WhenSubdomainsDisabled()
    {
        var service = CreateService(out var db);

        await service.RegisterAsync(new RegisterRequest("Saket Rasoi", "A", "a@test.com", "Password1", "919876543210"));

        db.Restaurants.Single().Subdomain.Should().BeNull();
    }

    [Fact]
    public async Task LoginAsync_LocksAfterRepeatedFailures()
    {
        var service = CreateService(out _);
        var email = $"lock-{Guid.NewGuid():N}@test.com";
        await service.RegisterAsync(new RegisterRequest("Lock Cafe", "A", email, "Password1", "919876543210"));

        for (var i = 0; i < 5; i++)
        {
            var wrong = () => service.LoginAsync(new LoginRequest(email, "wrong-password"));
            await wrong.Should().ThrowAsync<UnauthorizedAppException>();
        }

        var locked = () => service.LoginAsync(new LoginRequest(email, "Password1"));
        await locked.Should().ThrowAsync<TooManyAttemptsException>();
    }

    [Fact]
    public async Task ChangePasswordAsync_ChecksCurrentPassword_AndBumpsVersion()
    {
        var service = CreateService(out var db);
        var email = $"change-{Guid.NewGuid():N}@test.com";
        await service.RegisterAsync(new RegisterRequest("Change Cafe", "A", email, "Password1", "919876543210"));
        var user = db.Users.Single();

        var wrong = () => service.ChangePasswordAsync(user.Id, new ChangePasswordRequest("nope", "NewPassword1"));
        await wrong.Should().ThrowAsync<ConflictException>();

        var result = await service.ChangePasswordAsync(user.Id, new ChangePasswordRequest("Password1", "NewPassword1"));

        result.Token.Should().NotBeNullOrEmpty();
        user.PasswordVersion.Should().Be(1);
        var login = await service.LoginAsync(new LoginRequest(email, "NewPassword1"));
        login.Token.Should().NotBeNullOrEmpty();
    }
}

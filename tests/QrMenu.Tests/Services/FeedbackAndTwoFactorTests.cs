using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Options;
using QrMenu.Application.Common;
using QrMenu.Application.Common.Exceptions;
using QrMenu.Application.Feedbacks;
using QrMenu.Application.SuperAdmins;
using QrMenu.Domain.Entities;
using QrMenu.Infrastructure.Auth;
using QrMenu.Infrastructure.Persistence;
using QrMenu.Infrastructure.Security;
using QrMenu.Infrastructure.Services;
using QrMenu.Tests.Common;
using Xunit;

namespace QrMenu.Tests.Services;

public class FeedbackAndTwoFactorTests
{
    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static readonly IOptions<JwtSettings> Jwt =
        Options.Create(new JwtSettings { Secret = "test-secret-that-is-long-enough-1234567890", Issuer = "t", Audience = "t" });

    // ---------- TOTP ----------

    [Fact]
    public void Totp_MatchesTheRfc6238TestVector()
    {
        // RFC 6238 appendix B, SHA-1 key "12345678901234567890", T = 59 s -> 94287082 (last 6 digits).
        Totp.Code(Encoding.ASCII.GetBytes("12345678901234567890"), 59 / 30).Should().Be("287082");
    }

    [Fact]
    public void Totp_Base32_RoundTrips()
    {
        var bytes = Encoding.ASCII.GetBytes("12345678901234567890");
        Totp.FromBase32(Totp.ToBase32(bytes)).Should().Equal(bytes);
    }

    // ---------- Two-step login ----------

    private static (SuperAdminTwoFactorService Service, SuperAdminService Login, AppDbContext Db, SuperAdmin Admin, Clock Clock) TwoFactor()
    {
        var db = InMemoryDbFactory.Create();
        var hasher = new BcryptPasswordHasher();
        var admin = new SuperAdmin { Id = Guid.NewGuid(), Name = "Admin", Email = "admin@test.com", PasswordHash = hasher.Hash("Password@123") };
        db.SuperAdmins.Add(admin);
        db.SaveChanges();

        var clock = new Clock(new DateTimeOffset(2026, 10, 9, 10, 0, 0, TimeSpan.Zero));
        var challenges = new TwoFactorChallenges(Jwt, clock);
        var jwt = new JwtTokenService(Jwt);
        var service = new SuperAdminTwoFactorService(db, new SecretProtector(Jwt), challenges, hasher, jwt, clock);
        var login = new SuperAdminService(db, hasher, jwt, Options.Create(new QrMenu.Application.Subscriptions.SubscriptionSettings()), challenges);
        return (service, login, db, admin, clock);
    }

    private static string CodeAt(string groupedSecret, DateTimeOffset at) =>
        Totp.Code(Totp.FromBase32(groupedSecret.Replace(" ", "")), Totp.CurrentStep(at));

    [Fact]
    public async Task TwoFactor_SetupEnableLogin_CodesWorkOnce_RecoveryCodesWorkOnce()
    {
        var (service, login, _, admin, clock) = TwoFactor();

        var setup = await service.StartSetupAsync(admin.Id);
        setup.OtpAuthUri.Should().StartWith("otpauth://totp/QRenvo:");
        setup.QrPngDataUrl.Should().StartWith("data:image/png;base64,");

        await service.Invoking(s => s.EnableAsync(admin.Id, new EnableTwoFactorRequest("000000"))).Should().ThrowAsync<ConflictException>();
        var enabled = await service.EnableAsync(admin.Id, new EnableTwoFactorRequest(CodeAt(setup.Secret, clock.Now)));
        enabled.RecoveryCodes.Should().HaveCount(8);
        (await service.GetStatusAsync(admin.Id)).Should().Be(new TwoFactorStatusDto(true, 8));

        // Password step: no token, only a challenge.
        var first = await login.LoginAsync(new SuperAdminLoginRequest("admin@test.com", "Password@123"));
        first.RequiresTwoFactor.Should().BeTrue();
        first.Token.Should().BeEmpty();

        // The code already used to turn it on is refused (same time step).
        await service.Invoking(s => s.VerifyLoginAsync(new VerifyTwoFactorRequest(first.ChallengeToken!, CodeAt(setup.Secret, clock.Now))))
            .Should().ThrowAsync<UnauthorizedAppException>();

        // 30 seconds later the new code works, once.
        clock.Now = clock.Now.AddSeconds(30);
        var code = CodeAt(setup.Secret, clock.Now);
        (await service.VerifyLoginAsync(new VerifyTwoFactorRequest(first.ChallengeToken!, code))).Token.Should().NotBeEmpty();
        await service.Invoking(s => s.VerifyLoginAsync(new VerifyTwoFactorRequest(first.ChallengeToken!, code)))
            .Should().ThrowAsync<UnauthorizedAppException>();

        // A recovery code works once, with or without the dash.
        var recovery = enabled.RecoveryCodes[0];
        (await service.VerifyLoginAsync(new VerifyTwoFactorRequest(first.ChallengeToken!, recovery.Replace("-", "")))).Token.Should().NotBeEmpty();
        await service.Invoking(s => s.VerifyLoginAsync(new VerifyTwoFactorRequest(first.ChallengeToken!, recovery)))
            .Should().ThrowAsync<UnauthorizedAppException>();
        (await service.GetStatusAsync(admin.Id)).RecoveryCodesLeft.Should().Be(7);
    }

    [Fact]
    public async Task TwoFactor_ChallengeExpires_AndDisableNeedsPasswordAndCode()
    {
        var (service, login, _, admin, clock) = TwoFactor();
        var setup = await service.StartSetupAsync(admin.Id);
        var enabled = await service.EnableAsync(admin.Id, new EnableTwoFactorRequest(CodeAt(setup.Secret, clock.Now)));

        var first = await login.LoginAsync(new SuperAdminLoginRequest("admin@test.com", "Password@123"));
        clock.Now = clock.Now.AddMinutes(6);
        await service.Invoking(s => s.VerifyLoginAsync(new VerifyTwoFactorRequest(first.ChallengeToken!, CodeAt(setup.Secret, clock.Now))))
            .Should().ThrowAsync<UnauthorizedAppException>();
        (await service.Invoking(s => s.VerifyLoginAsync(new VerifyTwoFactorRequest("forged.123.abc", "123456")))
            .Should().ThrowAsync<UnauthorizedAppException>()).Which.Message.Should().Contain("expired");

        await service.Invoking(s => s.DisableAsync(admin.Id, new DisableTwoFactorRequest("wrong", enabled.RecoveryCodes[0])))
            .Should().ThrowAsync<ConflictException>();
        await service.DisableAsync(admin.Id, new DisableTwoFactorRequest("Password@123", enabled.RecoveryCodes[0]));

        (await service.GetStatusAsync(admin.Id)).Enabled.Should().BeFalse();
        (await login.LoginAsync(new SuperAdminLoginRequest("admin@test.com", "Password@123"))).Token.Should().NotBeEmpty();
    }

    // ---------- Feedback ----------

    private static (FeedbackService Service, AppDbContext Db, Restaurant Restaurant, Order Order) Feedback()
    {
        var db = InMemoryDbFactory.Create();
        var restaurant = new Restaurant
        {
            Id = Guid.NewGuid(), Name = "Saket Rasoi", Slug = "saket-rasoi", WhatsAppNumber = "919876543210",
            LogoUrl = "/uploads/11111111-1111-1111-1111-111111111111.png", IsActive = true
        };
        var order = new Order { Id = Guid.NewGuid(), RestaurantId = restaurant.Id, TableNumberSnapshot = "4", CustomerName = "Aarav", Total = 200 };
        db.Restaurants.Add(restaurant);
        db.Orders.Add(order);
        db.SaveChanges();
        var service = new FeedbackService(db, Options.Create(new SiteSettings { AppUrl = "https://app.qrenvo.com" }), TimeProvider.System);
        return (service, db, restaurant, order);
    }

    [Fact]
    public async Task CustomerFeedback_OnePerOrder_EditReplaces_SummaryCounts()
    {
        var (service, _, restaurant, order) = Feedback();

        (await service.GetForOrderAsync("saket-rasoi", order.Id)).Should().BeNull();
        var first = await service.SubmitForOrderAsync("saket-rasoi", order.Id, new SubmitCustomerFeedbackRequest(4, null, " Tasty! ", null));
        first.Name.Should().Be("Aarav");
        first.Comment.Should().Be("Tasty!");
        first.DisplayImageUrl.Should().BeNull();

        await service.SubmitForOrderAsync("saket-rasoi", order.Id, new SubmitCustomerFeedbackRequest(5, "Aarav S", null, null));

        var summary = await service.GetCustomerFeedbackAsync(restaurant.Id);
        summary.Count.Should().Be(1);
        summary.Average.Should().Be(5);
        summary.CountByStars.Should().Equal(0, 0, 0, 0, 1);
        summary.Items[0].TableNumber.Should().Be("4");

        await service.Invoking(s => s.SubmitForOrderAsync("other-place", order.Id, new SubmitCustomerFeedbackRequest(5, null, null, null)))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task OwnerFeedback_UsesLogo_OnlyPublishedOnLanding_EditGoesBackToReview()
    {
        var (service, _, restaurant, order) = Feedback();

        var saved = await service.SaveOwnerFeedbackAsync(restaurant.Id, new SubmitOwnerFeedbackRequest(5, "Ramesh", "Orders are faster now.", null));
        saved.DisplayImageUrl.Should().Be(restaurant.LogoUrl);
        saved.IsPublished.Should().BeFalse();
        (await service.GetTestimonialsAsync()).Should().BeEmpty();

        await service.SetPublishedAsync(saved.Id, true);
        var guest = await service.SubmitForOrderAsync("saket-rasoi", order.Id, new SubmitCustomerFeedbackRequest(4, "Priya", "Nice", null));
        await service.SetPublishedAsync(guest.Id, true);

        var landing = await service.GetTestimonialsAsync();
        landing.Should().HaveCount(2);
        landing[0].Role.Should().Be("Owner, Saket Rasoi");
        landing[0].ImageUrl.Should().Be("https://app.qrenvo.com/uploads/w160/11111111-1111-1111-1111-111111111111.png");
        landing[1].Role.Should().Be("Guest at Saket Rasoi");

        // Saving the same words keeps it published; changed words go back to review.
        await service.SaveOwnerFeedbackAsync(restaurant.Id, new SubmitOwnerFeedbackRequest(5, "Ramesh", "Orders are faster now.", null));
        (await service.GetOwnerFeedbackAsync(restaurant.Id))!.IsPublished.Should().BeTrue();
        await service.SaveOwnerFeedbackAsync(restaurant.Id, new SubmitOwnerFeedbackRequest(4, "Ramesh", "Changed my mind", null));
        (await service.GetOwnerFeedbackAsync(restaurant.Id))!.IsPublished.Should().BeFalse();
    }

    [Fact]
    public void FeedbackPhoto_MustBeOurOwnUpload()
    {
        var validator = new SubmitCustomerFeedbackRequestValidator();
        validator.Validate(new SubmitCustomerFeedbackRequest(5, null, null, "/uploads/abc-123.jpg")).IsValid.Should().BeTrue();
        validator.Validate(new SubmitCustomerFeedbackRequest(5, null, null, "https://evil.example/x.jpg")).IsValid.Should().BeFalse();
        validator.Validate(new SubmitCustomerFeedbackRequest(5, null, null, "/uploads/../appsettings.json")).IsValid.Should().BeFalse();
        validator.Validate(new SubmitCustomerFeedbackRequest(0, null, null, null)).IsValid.Should().BeFalse();
    }
}

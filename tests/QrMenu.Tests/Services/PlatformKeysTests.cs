using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using QrMenu.Application.Common;
using QrMenu.Application.Common.Interfaces;
using QrMenu.Application.Platform;
using QrMenu.Domain.Entities;
using QrMenu.Infrastructure.Auth;
using QrMenu.Infrastructure.Payments;
using QrMenu.Infrastructure.Persistence;
using QrMenu.Infrastructure.Security;
using QrMenu.Infrastructure.Services;
using QrMenu.Tests.Common;
using Xunit;

namespace QrMenu.Tests.Services;

/// <summary>Keys saved from the super admin panel: encrypted at rest, never shown back, and they win over the .env.</summary>
public class PlatformKeysTests
{
    private static readonly SecretProtector Protector =
        new(Options.Create(new JwtSettings { Secret = "test-secret-that-is-long-enough-1234567890" }));

    private sealed class FakeGateway : IPaymentGateway
    {
        public bool IsConfigured => true;
        public string KeyId => "";
        public Task<string> CreateOrderAsync(long amountInPaise, string currency, string receipt, IDictionary<string, string> notes, CancellationToken ct = default) =>
            Task.FromResult("order");
        public bool IsPaymentSignatureValid(string orderId, string paymentId, string signature) => true;
        public bool IsWebhookSignatureValid(string body, string? signature) => true;
        public Task<GatewayCheckResult> CheckCredentialsAsync(string keyId, string keySecret, CancellationToken ct = default) =>
            Task.FromResult(new GatewayCheckResult(keySecret == "right", keyId));
        public Task<long?> GetPaymentFeeAsync(string paymentId, CancellationToken ct = default) => Task.FromResult<long?>(null);
        public Task<GatewayRefundResult> RefundAsync(string paymentId, long amountInPaise, string receipt, string? orderId, CancellationToken ct = default) =>
            Task.FromResult(new GatewayRefundResult("rfnd_test", "processed"));
    }

    private static (PlatformKeysService Service, IntegrationKeyStore Store, AppDbContext Db) Create()
    {
        var db = InMemoryDbFactory.Create();
        var store = new IntegrationKeyStore(
            Options.Create(new RazorpaySettings { KeyId = "rzp_test_server", KeySecret = "server-secret-1111" }),
            new ConfigurationBuilder().Build());
        var service = new PlatformKeysService(db, Protector, store, new FakeGateway(),
            Options.Create(new SiteSettings { AppUrl = "https://app.qrenvo.com" }));
        return (service, store, db);
    }

    [Fact]
    public void Protector_RoundTrips_AndRejectsTamperedText()
    {
        var protectedText = Protector.Protect("sk_live_abc");

        protectedText.Should().NotContain("sk_live_abc");
        Protector.Unprotect(protectedText).Should().Be("sk_live_abc");
        Protector.Unprotect(protectedText[..^4] + "AAAA").Should().BeNull();
        Protector.Unprotect(null).Should().BeNull();
    }

    [Fact]
    public async Task PanelKeys_WinOverServer_AreStoredEncrypted_AndOnlyLastFourIsShown()
    {
        var (service, store, db) = Create();

        var before = await service.GetAsync();
        before.RazorpayKeyIdSource.Should().Be("server");
        before.RazorpayKeySecret.Source.Should().Be("server");
        before.GeminiApiKey.IsSet.Should().BeFalse();

        var after = await service.UpdateAsync(
            new UpdatePlatformKeysRequest("rzp_live_panel", "panel-secret-9876", null, "gemini-key-5555"), "admin@test.com");

        store.RazorpayKeyId.Should().Be("rzp_live_panel");
        store.RazorpayKeySecret.Should().Be("panel-secret-9876");
        store.GeminiApiKey.Should().Be("gemini-key-5555");
        after.RazorpayMode.Should().Be("live");
        after.RazorpayKeySecret.Should().Be(new SecretKeyStatus(true, "panel", "9876"));
        after.WebhookUrl.Should().Be("https://app.qrenvo.com/api/payments/razorpay/webhook");

        var row = db.PlatformSettings.Single();
        row.RazorpayKeySecretEncrypted.Should().NotBeNull().And.NotContain("panel-secret");
        row.GeminiApiKeyEncrypted.Should().NotContain("gemini-key");
    }

    [Fact]
    public async Task EmptyValue_RemovesThePanelKey_SoTheServerKeyAppliesAgain_NullLeavesItAlone()
    {
        var (service, store, _) = Create();
        await service.UpdateAsync(new UpdatePlatformKeysRequest("rzp_test_panel", "panel-secret-9876", null, null), "a");

        await service.UpdateAsync(new UpdatePlatformKeysRequest(null, "", null, null), "a");

        store.RazorpayKeyId.Should().Be("rzp_test_panel");
        store.RazorpayKeySecret.Should().Be("server-secret-1111");
    }

    [Fact]
    public async Task SavedKeys_AreLoadedIntoMemoryAtStartup()
    {
        var (service, _, db) = Create();
        await service.UpdateAsync(new UpdatePlatformKeysRequest(null, "panel-secret-9876", null, null), "a");

        var freshStore = new IntegrationKeyStore(Options.Create(new RazorpaySettings()), new ConfigurationBuilder().Build());
        await new PlatformKeysService(db, Protector, freshStore, new FakeGateway(), Options.Create(new SiteSettings())).LoadAsync();

        freshStore.RazorpayKeySecret.Should().Be("panel-secret-9876");
    }

    [Fact]
    public async Task TestRazorpay_UsesTypedKeys_OrFallsBackToTheKeysInUse()
    {
        var (service, _, _) = Create();

        (await service.TestRazorpayAsync(new TestRazorpayKeysRequest("rzp_test_typed", "right"))).Should()
            .Be(new KeyCheckResultDto(true, "rzp_test_typed"));
        (await service.TestRazorpayAsync(new TestRazorpayKeysRequest(null, null))).Should()
            .Be(new KeyCheckResultDto(false, "rzp_test_server"));
    }

    [Fact]
    public async Task DemoAutoReset_FirstRunStartsCounting_AndNothingHappensOutsideTheDemo()
    {
        var db = InMemoryDbFactory.Create();
        DemoResetService Service(string environment) => new(db, Options.Create(new SiteSettings { Environment = environment }),
            null!, null!, TimeProvider.System, NullLogger<DemoResetService>.Instance);

        (await Service("Prod").ResetIfDueAsync()).Should().BeFalse();
        db.PlatformSettings.Should().BeEmpty();

        (await Service("Demo").ResetIfDueAsync()).Should().BeFalse();
        var status = await Service("Demo").GetStatusAsync();
        status.LastResetAt.Should().NotBeNull();
        status.NextResetAt.Should().Be(status.LastResetAt!.Value.AddDays(30));

        await Service("Prod").Invoking(s => s.ResetAsync("a")).Should().ThrowAsync<QrMenu.Application.Common.Exceptions.ForbiddenException>();
    }

    [Fact]
    public async Task DemoAutoReset_ZeroDays_IsSavedAsOff()
    {
        var db = InMemoryDbFactory.Create();
        var service = new DemoResetService(db, Options.Create(new SiteSettings { Environment = "Demo" }),
            null!, null!, TimeProvider.System, NullLogger<DemoResetService>.Instance);

        var status = await service.UpdateSettingsAsync(new UpdateDemoSettingsRequest(0), "a");

        status.AutoResetDays.Should().Be(0);
        status.NextResetAt.Should().BeNull();
        db.PlatformSettings.Single().DemoAutoResetDays.Should().Be(0);
    }
}

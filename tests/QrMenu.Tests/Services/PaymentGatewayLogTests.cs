using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using QrMenu.Application.Common.Exceptions;
using QrMenu.Application.Common.Interfaces;
using QrMenu.Application.Subscriptions;
using QrMenu.Infrastructure.Auth;
using QrMenu.Infrastructure.Payments;
using QrMenu.Infrastructure.Persistence;
using QrMenu.Infrastructure.Services;
using Xunit;

namespace QrMenu.Tests.Services;

/// <summary>Every Razorpay message is kept for the super admin, also the refused ones.</summary>
public class PaymentGatewayLogTests
{
    private sealed class RefusingGateway : IPaymentGateway
    {
        public bool IsConfigured => true;
        public string KeyId => "rzp_test_x";
        public Task<string> CreateOrderAsync(long amountInPaise, string currency, string receipt, IDictionary<string, string> notes, CancellationToken ct = default) =>
            Task.FromResult("order_x");
        public bool IsPaymentSignatureValid(string orderId, string paymentId, string signature) => false;
        public bool IsWebhookSignatureValid(string body, string? signature) => false;
        public Task<GatewayCheckResult> CheckCredentialsAsync(string keyId, string keySecret, CancellationToken ct = default) =>
            Task.FromResult(new GatewayCheckResult(true, "ok"));
    }

    private static (OnlinePaymentService Service, AppDbContext Db, SuperAdminService Admin) Create()
    {
        var name = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(name));
        var provider = services.BuildServiceProvider();

        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(name).Options);
        var writer = new PaymentGatewayLogWriter(provider.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System,
            NullLogger<PaymentGatewayLogWriter>.Instance);
        var service = new OnlinePaymentService(db, new RefusingGateway(), null!, Options.Create(new SubscriptionSettings()),
            TimeProvider.System, NullLogger<OnlinePaymentService>.Instance, writer);
        var admin = new SuperAdminService(db, new BcryptPasswordHasher(), null!, Options.Create(new SubscriptionSettings()), null!);
        return (service, db, admin);
    }

    [Fact]
    public async Task RefusedWebhook_IsLoggedWithItsRawBody_AndLinkedToTheOrder()
    {
        var (service, _, admin) = Create();
        const string body = "{\"event\":\"payment.captured\",\"payload\":{\"payment\":{\"entity\":{\"id\":\"pay_1\",\"order_id\":\"order_abc\",\"amount\":99900}}}}";

        (await service.HandleWebhookAsync(body, "bad-signature")).Should().BeFalse();

        var log = await admin.GetGatewayLogAsync("order_abc");
        log.Should().ContainSingle();
        log[0].Kind.Should().Be("webhook");
        log[0].StatusCode.Should().Be(400);
        log[0].RequestBody.Should().Be(body);
        log[0].Note.Should().Contain("payment.captured").And.Contain("INVALID");
    }

    [Fact]
    public async Task RefusedCheckoutConfirmation_IsLogged()
    {
        var (service, _, admin) = Create();

        await service.Invoking(s => s.ConfirmCheckoutAsync(Guid.NewGuid(), new ConfirmCheckoutRequest("order_def", "pay_9", "sig")))
            .Should().ThrowAsync<ConflictException>();

        var log = await admin.GetGatewayLogAsync("order_def");
        log.Should().ContainSingle();
        log[0].Kind.Should().Be("checkout.confirm");
        log[0].RequestBody.Should().Contain("pay_9");
        log[0].Note.Should().Contain("INVALID");
    }
}

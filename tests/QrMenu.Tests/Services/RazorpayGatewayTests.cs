using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using QrMenu.Infrastructure.Payments;
using QrMenu.Infrastructure.Security;
using Xunit;

namespace QrMenu.Tests.Services;

public class RazorpayGatewayTests
{
    private static RazorpayGateway Create(string webhookSecret = "whsec") => new(
        new HttpClient(),
        new IntegrationKeyStore(
            Options.Create(new RazorpaySettings { KeyId = "rzp_test_key", KeySecret = "secret", WebhookSecret = webhookSecret }),
            new ConfigurationBuilder().Build()),
        NullLogger<RazorpayGateway>.Instance);

    private static string Sign(string payload, string secret) =>
        Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();

    [Fact]
    public void PaymentSignature_MatchesOnlyTheRightOrderAndPayment()
    {
        var gateway = Create();
        var signature = Sign("order_1|pay_1", "secret");

        gateway.IsPaymentSignatureValid("order_1", "pay_1", signature).Should().BeTrue();
        gateway.IsPaymentSignatureValid("order_2", "pay_1", signature).Should().BeFalse();
        gateway.IsPaymentSignatureValid("order_1", "pay_1", "deadbeef").Should().BeFalse();
    }

    [Fact]
    public void WebhookSignature_RequiresTheWebhookSecret()
    {
        const string body = "{\"event\":\"payment.captured\"}";

        Create().IsWebhookSignatureValid(body, Sign(body, "whsec")).Should().BeTrue();
        Create().IsWebhookSignatureValid(body, Sign(body, "secret")).Should().BeFalse();
        Create(webhookSecret: "").IsWebhookSignatureValid(body, Sign(body, "")).Should().BeFalse();
    }
}

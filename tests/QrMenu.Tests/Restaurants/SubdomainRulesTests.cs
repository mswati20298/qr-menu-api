using FluentAssertions;
using QrMenu.Application.Common;
using QrMenu.Application.Restaurants;
using Xunit;

namespace QrMenu.Tests.Restaurants;

public class SubdomainRulesTests
{
    [Theory]
    [InlineData("saket")]
    [InlineData("saket-rasoi")]
    [InlineData("cafe24")]
    public void Problem_AcceptsValidNames(string name) => SubdomainRules.Problem(name).Should().BeNull();

    [Theory]
    [InlineData("ab")]
    [InlineData("-saket")]
    [InlineData("saket-")]
    [InlineData("sa--ket")]
    [InlineData("Saket")]
    [InlineData("sa ket")]
    [InlineData("app")]
    [InlineData("demo")]
    [InlineData("admin")]
    public void Problem_RejectsInvalidOrReservedNames(string name) => SubdomainRules.Problem(name).Should().NotBeNull();

    [Theory]
    [InlineData("saket.qrenvo.com", "saket")]
    [InlineData("SAKET.qrenvo.com:443", "saket")]
    [InlineData("app.qrenvo.com", null)]
    [InlineData("qrenvo.com", null)]
    [InlineData("a.b.qrenvo.com", null)]
    [InlineData("saket.evil.com", null)]
    [InlineData("saketqrenvo.com", null)]
    public void FromHost_ReadsOneLevelRestaurantLabel(string host, string? expected) =>
        SubdomainRules.FromHost(host, "qrenvo.com").Should().Be(expected);

    [Fact]
    public void MenuLinks_UseSubdomainWhenSet()
    {
        var site = new SiteSettings { RootDomain = "qrenvo.com", AppUrl = "https://app.qrenvo.com" };

        MenuLinks.MenuUrl(site, "saket-rasoi", "saket").Should().Be("https://saket.qrenvo.com");
        MenuLinks.TableUrl(site, "saket-rasoi", "saket", "T 5").Should().Be("https://saket.qrenvo.com/?t=T%205");
        MenuLinks.MenuUrl(site, "saket-rasoi", null).Should().Be("https://app.qrenvo.com/m/saket-rasoi");
    }

    [Fact]
    public void MenuLinks_IgnoreSubdomainWhenDisabled()
    {
        var site = new SiteSettings { AppUrl = "https://demo.qrenvo.com/" };

        MenuLinks.TableUrl(site, "saket-rasoi", "saket", "5").Should().Be("https://demo.qrenvo.com/m/saket-rasoi?t=5");
    }
}

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using QrMenu.Domain.Entities;
using QrMenu.Infrastructure.Auth;
using Xunit;

namespace QrMenu.Tests.Services;

/// <summary>Each kind of token reaches only its own endpoints.</summary>
public class AuthorizationPolicyTests
{
    private static readonly JwtSettings Settings = new()
    {
        Secret = "test-secret-key-that-is-at-least-32-characters-long",
        Issuer = "TestIssuer",
        Audience = "TestAudience",
        ExpiryMinutes = 60
    };

    private static ClaimsPrincipal Read(string token)
    {
        var parameters = new TokenValidationParameters
        {
            ValidIssuer = Settings.Issuer,
            ValidAudience = Settings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Settings.Secret))
        };
        return new JwtSecurityTokenHandler().ValidateToken(token, parameters, out _);
    }

    private static async Task<bool> Allowed(ClaimsPrincipal user, string policy)
    {
        var services = new ServiceCollection().AddLogging().AddAuthorization(AuthorizationPolicies.Configure).BuildServiceProvider();
        var result = await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user, policy);
        return result.Succeeded;
    }

    [Fact]
    public async Task Tokens_OnlyReachTheirOwnEndpoints()
    {
        var jwt = new JwtTokenService(Options.Create(Settings));
        var restaurant = new Restaurant { Id = Guid.NewGuid(), Name = "R", Slug = "r" };
        var owner = Read(jwt.GenerateToken(new User { Id = Guid.NewGuid(), RestaurantId = restaurant.Id, Name = "O", Email = "o@x.com" }));
        var kitchen = Read(jwt.GenerateKitchenToken(restaurant));
        var admin = Read(jwt.GenerateSuperAdminToken(new SuperAdmin { Id = Guid.NewGuid(), Name = "A", Email = "a@x.com" }));

        (await Allowed(owner, AuthorizationPolicies.Owner)).Should().BeTrue();
        (await Allowed(owner, AuthorizationPolicies.Kitchen)).Should().BeTrue();
        (await Allowed(owner, AuthorizationPolicies.SuperAdmin)).Should().BeFalse();

        (await Allowed(kitchen, AuthorizationPolicies.Kitchen)).Should().BeTrue();
        (await Allowed(kitchen, AuthorizationPolicies.Owner)).Should().BeFalse();
        (await Allowed(kitchen, AuthorizationPolicies.SuperAdmin)).Should().BeFalse();

        (await Allowed(admin, AuthorizationPolicies.SuperAdmin)).Should().BeTrue();
        (await Allowed(admin, AuthorizationPolicies.Owner)).Should().BeFalse();
        (await Allowed(admin, AuthorizationPolicies.Kitchen)).Should().BeFalse();
    }
}

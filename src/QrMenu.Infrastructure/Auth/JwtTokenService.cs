using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using QrMenu.Application.Common.Interfaces;
using QrMenu.Domain.Entities;

namespace QrMenu.Infrastructure.Auth;

public class JwtTokenService(IOptions<JwtSettings> settings) : IJwtTokenService
{
    private readonly JwtSettings _settings = settings.Value;

    public string GenerateToken(User user)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.Name),
            new Claim("restaurantId", user.RestaurantId.ToString()),
            // Goes up when the password changes or is reset, which signs out every old login (ActiveRestaurantFilter).
            new Claim("pwdv", user.PasswordVersion.ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_settings.ExpiryMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateSuperAdminToken(SuperAdmin admin)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, admin.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, admin.Id.ToString()),
            new Claim(ClaimTypes.Email, admin.Email),
            new Claim(ClaimTypes.Name, admin.Name),
            new Claim("superAdmin", "true"),
            new Claim("pwdv", admin.PasswordVersion.ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // Higher privilege, shorter session: at most 8 hours.
        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(Math.Min(_settings.ExpiryMinutes, 480)),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateKitchenToken(Restaurant restaurant)
    {
        // No user id and no email: this token can only reach the /api/kitchen endpoints.
        // kitchenVersion lets a PIN change sign every kitchen screen out (checked by ActiveRestaurantFilter).
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, $"kitchen:{restaurant.Id}"),
            new Claim(ClaimTypes.Name, "Kitchen"),
            new Claim("restaurantId", restaurant.Id.ToString()),
            new Claim("kitchen", "true"),
            new Claim("kitchenVersion", restaurant.KitchenPinVersion.ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // Long enough for a full service day on the kitchen tablet.
        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(16),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

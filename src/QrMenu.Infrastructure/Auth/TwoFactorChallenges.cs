using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace QrMenu.Infrastructure.Auth;

/// <summary>
/// Short-lived proof that the password step passed, carried to the 6-digit-code step:
/// "{adminId:N}.{expiresUnix}.{HMAC}". Signed with the JWT secret; worthless after 5 minutes.
/// </summary>
public class TwoFactorChallenges(IOptions<JwtSettings> jwtOptions, TimeProvider clock)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);
    private readonly byte[] _key = SHA256.HashData(Encoding.UTF8.GetBytes("qrenvo:2fa-challenge:" + jwtOptions.Value.Secret));

    public string Issue(Guid adminId)
    {
        var payload = $"{adminId:N}.{clock.GetUtcNow().Add(Lifetime).ToUnixTimeSeconds()}";
        return $"{payload}.{Sign(payload)}";
    }

    /// <summary>The admin the challenge was issued for, or null when it is forged or expired.</summary>
    public Guid? Read(string? token)
    {
        var parts = (token ?? string.Empty).Split('.');
        if (parts.Length != 3 || !Guid.TryParseExact(parts[0], "N", out var adminId) || !long.TryParse(parts[1], out var expires))
        {
            return null;
        }

        var expected = Encoding.ASCII.GetBytes(Sign($"{parts[0]}.{parts[1]}"));
        if (!CryptographicOperations.FixedTimeEquals(expected, Encoding.ASCII.GetBytes(parts[2])))
        {
            return null;
        }

        return clock.GetUtcNow().ToUnixTimeSeconds() <= expires ? adminId : null;
    }

    private string Sign(string payload) =>
        Convert.ToHexString(HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
}

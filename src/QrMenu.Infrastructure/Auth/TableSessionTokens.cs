using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using QrMenu.Application.Common.Interfaces;

namespace QrMenu.Infrastructure.Auth;

/// <summary>
/// Token format: <c>{tableId:N}.{expiry unix seconds}.{HMAC-SHA256 signature}</c>. The signature covers the
/// table's QR code as well, so a reset code invalidates old tokens without storing any sessions.
/// </summary>
public class TableSessionTokens(IOptions<JwtSettings> settings) : ITableSessionTokens
{
    // A separate purpose string keeps these signatures from ever matching anything else signed with the secret.
    private readonly byte[] _key = SHA256.HashData(Encoding.UTF8.GetBytes("qrenvo-table-session:" + settings.Value.Secret));

    public string Create(Guid tableId, string tableCode, DateTime expiresUtc)
    {
        var payload = $"{tableId:N}.{new DateTimeOffset(DateTime.SpecifyKind(expiresUtc, DateTimeKind.Utc)).ToUnixTimeSeconds()}";
        return $"{payload}.{Sign(payload, tableCode)}";
    }

    public (Guid TableId, DateTime ExpiresUtc)? Read(string token)
    {
        var parts = token?.Split('.') ?? [];
        if (parts.Length != 3 || !Guid.TryParseExact(parts[0], "N", out var tableId) || !long.TryParse(parts[1], out var unix))
        {
            return null;
        }

        return (tableId, DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime);
    }

    public bool IsValid(string token, Guid tableId, string tableCode, DateTime nowUtc)
    {
        var read = Read(token);
        if (read is null || read.Value.TableId != tableId || read.Value.ExpiresUtc <= nowUtc)
        {
            return false;
        }

        var lastDot = token.LastIndexOf('.');
        var expected = Encoding.ASCII.GetBytes(Sign(token[..lastDot], tableCode));
        var given = Encoding.ASCII.GetBytes(token[(lastDot + 1)..]);
        return CryptographicOperations.FixedTimeEquals(expected, given);
    }

    private string Sign(string payload, string tableCode)
    {
        var mac = HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes($"{payload}.{tableCode.ToUpperInvariant()}"));
        return Convert.ToBase64String(mac).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}

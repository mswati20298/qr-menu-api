using System.Security.Cryptography;
using System.Text;

namespace QrMenu.Infrastructure.Auth;

/// <summary>
/// Time-based one-time codes (RFC 6238: 6 digits, 30 seconds, HMAC-SHA1), the same codes Google Authenticator,
/// Microsoft Authenticator and Authy show.
/// </summary>
public static class Totp
{
    private const int Digits = 6;
    private const int StepSeconds = 30;
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    /// <summary>A new random 160-bit secret, Base32 as the apps expect it.</summary>
    public static string NewSecret() => ToBase32(RandomNumberGenerator.GetBytes(20));

    public static long CurrentStep(DateTimeOffset now) => now.ToUnixTimeSeconds() / StepSeconds;

    /// <summary>
    /// The time step the code belongs to, or null. One step either side is accepted for phone clocks that are slightly off.
    /// The caller refuses a step it has already accepted, so a code cannot be used twice.
    /// </summary>
    public static long? Verify(string secret, string? code, DateTimeOffset now)
    {
        var digits = new string((code ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits.Length != Digits)
        {
            return null;
        }

        var key = FromBase32(secret);
        var current = CurrentStep(now);
        for (var offset = -1; offset <= 1; offset++)
        {
            var step = current + offset;
            var expected = Encoding.ASCII.GetBytes(Code(key, step));
            if (CryptographicOperations.FixedTimeEquals(expected, Encoding.ASCII.GetBytes(digits)))
            {
                return step;
            }
        }
        return null;
    }

    public static string Code(byte[] key, long step)
    {
        Span<byte> counter = stackalloc byte[8];
        for (var i = 7; i >= 0; i--)
        {
            counter[i] = (byte)(step & 0xff);
            step >>= 8;
        }

        var hash = HMACSHA1.HashData(key, counter);
        var offset = hash[^1] & 0x0f;
        var binary = ((hash[offset] & 0x7f) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
        return (binary % 1_000_000).ToString("D6");
    }

    /// <summary>otpauth:// link the authenticator apps read from a QR code.</summary>
    public static string OtpAuthUri(string issuer, string account, string secret) =>
        $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(account)}" +
        $"?secret={secret}&issuer={Uri.EscapeDataString(issuer)}&algorithm=SHA1&digits={Digits}&period={StepSeconds}";

    public static string ToBase32(byte[] data)
    {
        var output = new StringBuilder();
        int buffer = 0, bits = 0;
        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                output.Append(Base32Alphabet[(buffer >> (bits - 5)) & 31]);
                bits -= 5;
            }
        }
        if (bits > 0)
        {
            output.Append(Base32Alphabet[(buffer << (5 - bits)) & 31]);
        }
        return output.ToString();
    }

    public static byte[] FromBase32(string text)
    {
        var clean = text.Trim().TrimEnd('=').Replace(" ", string.Empty).ToUpperInvariant();
        var output = new List<byte>();
        int buffer = 0, bits = 0;
        foreach (var c in clean)
        {
            var value = Base32Alphabet.IndexOf(c);
            if (value < 0)
            {
                throw new FormatException("Not a Base32 secret.");
            }
            buffer = (buffer << 5) | value;
            bits += 5;
            if (bits >= 8)
            {
                output.Add((byte)((buffer >> (bits - 8)) & 0xff));
                bits -= 8;
            }
        }
        return [.. output];
    }
}

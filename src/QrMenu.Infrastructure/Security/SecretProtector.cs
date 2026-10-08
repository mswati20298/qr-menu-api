using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using QrMenu.Infrastructure.Auth;

namespace QrMenu.Infrastructure.Security;

/// <summary>
/// Encrypts keys saved from the super admin panel (AES-GCM). The encryption key is derived from the JWT secret,
/// which lives only in the server's environment, so a copy of the database alone does not reveal them.
/// If the JWT secret is ever changed, the saved keys can no longer be read and must be entered again.
/// </summary>
public class SecretProtector(IOptions<JwtSettings> jwtOptions)
{
    private const string Prefix = "v1:";
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[] _key = SHA256.HashData(Encoding.UTF8.GetBytes("qrenvo:platform-keys:" + jwtOptions.Value.Secret));

    public string Protect(string plainText)
    {
        var plain = Encoding.UTF8.GetBytes(plainText);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var cipher = new byte[plain.Length];
        var tag = new byte[TagSize];

        using (var aes = new AesGcm(_key, TagSize))
        {
            aes.Encrypt(nonce, plain, cipher, tag);
        }

        return Prefix + Convert.ToBase64String([.. nonce, .. tag, .. cipher]);
    }

    /// <summary>Null when the value is empty or cannot be decrypted (e.g. the JWT secret was changed).</summary>
    public string? Unprotect(string? protectedText)
    {
        if (string.IsNullOrEmpty(protectedText) || !protectedText.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            var data = Convert.FromBase64String(protectedText[Prefix.Length..]);
            var nonce = data.AsSpan(0, NonceSize);
            var tag = data.AsSpan(NonceSize, TagSize);
            var cipher = data.AsSpan(NonceSize + TagSize);
            var plain = new byte[cipher.Length];

            using var aes = new AesGcm(_key, TagSize);
            aes.Decrypt(nonce, cipher, tag, plain);
            return Encoding.UTF8.GetString(plain);
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException or ArgumentException)
        {
            return null;
        }
    }
}

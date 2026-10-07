using System.Security.Cryptography;
using System.Text;

namespace QrMenu.Application.Tables;

/// <summary>
/// The secret code printed in a table's QR (<c>?t=5&amp;k=7QX4PM9KD2TA</c>). Without it a link can show the menu
/// but cannot order for that table, so nobody can order "for table 5" from home by typing a URL.
/// </summary>
public static class TableCodes
{
    public const int Length = 12;

    // No look-alikes (0/O, 1/I/L), so a code read aloud or typed from a card is never ambiguous.
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    public static string New() => RandomNumberGenerator.GetString(Alphabet, Length);

    /// <summary>Case-insensitive, constant-time comparison (no timing hints about how much of a guess was right).</summary>
    public static bool Matches(string? expected, string? given)
    {
        if (string.IsNullOrEmpty(expected) || string.IsNullOrWhiteSpace(given))
        {
            return false;
        }

        var a = Encoding.UTF8.GetBytes(expected.ToUpperInvariant());
        var b = Encoding.UTF8.GetBytes(given.Trim().ToUpperInvariant());
        return CryptographicOperations.FixedTimeEquals(a, b);
    }
}

namespace QrMenu.Application.Common.Interfaces;

/// <summary>
/// Signed "this phone scanned table X's QR" tokens. A token names the table and an expiry, and is signed
/// together with the table's current QR code, so resetting the code ends every session started with it.
/// </summary>
public interface ITableSessionTokens
{
    string Create(Guid tableId, string tableCode, DateTime expiresUtc);

    /// <summary>The table and expiry written in the token, or null when it is malformed. Does not check the signature.</summary>
    (Guid TableId, DateTime ExpiresUtc)? Read(string token);

    /// <summary>True when the signature matches the table's current code and the token has not expired.</summary>
    bool IsValid(string token, Guid tableId, string tableCode, DateTime nowUtc);
}

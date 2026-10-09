namespace QrMenu.Domain.Entities;

/// <summary>Platform administrator. Not tied to any restaurant; can only use the /api/superadmin endpoints.</summary>
public class SuperAdmin
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Goes up when the password changes; tokens issued before stop working.</summary>
    public int PasswordVersion { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Two-step login with an authenticator app (6-digit codes). Secrets are stored encrypted.
    public bool TwoFactorEnabled { get; set; }
    public string? TwoFactorSecretEncrypted { get; set; }

    /// <summary>Secret shown during setup, until the first code confirms the app has it.</summary>
    public string? PendingTwoFactorSecretEncrypted { get; set; }

    /// <summary>SHA-256 hashes of the unused one-time recovery codes, comma separated.</summary>
    public string? RecoveryCodeHashes { get; set; }

    /// <summary>Time step of the last accepted code, so the same code cannot be used twice.</summary>
    public long TwoFactorLastStep { get; set; }
}

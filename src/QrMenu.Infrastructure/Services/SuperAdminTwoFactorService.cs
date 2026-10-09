using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using QrMenu.Application.Common.Exceptions;
using QrMenu.Application.Common.Interfaces;
using QrMenu.Application.SuperAdmins;
using QrMenu.Domain.Entities;
using QrMenu.Infrastructure.Auth;
using QrMenu.Infrastructure.Persistence;
using QrMenu.Infrastructure.Security;

namespace QrMenu.Infrastructure.Services;

public class SuperAdminTwoFactorService(
    AppDbContext db,
    SecretProtector protector,
    TwoFactorChallenges challenges,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService,
    TimeProvider clock) : ISuperAdminTwoFactorService
{
    private const string Issuer = "QRenvo";
    private const int RecoveryCodeCount = 8;

    // No look-alike characters, so a printed code can be typed back without mistakes.
    private const string RecoveryAlphabet = "abcdefghjkmnpqrstuvwxyz23456789";

    public async Task<SuperAdminAuthResponse> VerifyLoginAsync(VerifyTwoFactorRequest request, CancellationToken ct = default)
    {
        var adminId = challenges.Read(request.ChallengeToken)
            ?? throw new UnauthorizedAppException("This sign-in has expired. Please enter your password again.");

        var lockKey = $"2fa:{adminId:N}";
        if (LoginAttemptTracker.IsLocked(lockKey))
        {
            throw new TooManyAttemptsException("Too many wrong codes. Please try again in a few minutes.");
        }

        var admin = await db.SuperAdmins.FirstOrDefaultAsync(a => a.Id == adminId, ct)
            ?? throw new UnauthorizedAppException("This sign-in has expired. Please enter your password again.");

        if (!admin.TwoFactorEnabled || !CheckCode(admin, request.Code, allowRecovery: true))
        {
            LoginAttemptTracker.RecordFailure(lockKey);
            throw new UnauthorizedAppException("That code is not right. Check the time on your phone and try again.");
        }

        await db.SaveChangesAsync(ct);
        LoginAttemptTracker.Reset(lockKey);
        return new SuperAdminAuthResponse(jwtTokenService.GenerateSuperAdminToken(admin), admin.Name);
    }

    public async Task<TwoFactorStatusDto> GetStatusAsync(Guid adminId, CancellationToken ct = default)
    {
        var admin = await FindAsync(adminId, ct);
        return new TwoFactorStatusDto(admin.TwoFactorEnabled, RecoveryHashes(admin).Count);
    }

    public async Task<TwoFactorSetupDto> StartSetupAsync(Guid adminId, CancellationToken ct = default)
    {
        var admin = await FindAsync(adminId, ct);
        if (admin.TwoFactorEnabled)
        {
            throw new ConflictException("Two-step login is already on. Turn it off first to use a new phone.");
        }

        var secret = Totp.NewSecret();
        admin.PendingTwoFactorSecretEncrypted = protector.Protect(secret);
        await db.SaveChangesAsync(ct);

        var uri = Totp.OtpAuthUri(Issuer, admin.Email, secret);
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(uri, QRCodeGenerator.ECCLevel.M);
        var png = new PngByteQRCode(data).GetGraphic(8);

        // Grouped in fours, as the apps show it, for typing by hand.
        var grouped = string.Join(' ', Enumerable.Range(0, (secret.Length + 3) / 4).Select(i => secret.Substring(i * 4, Math.Min(4, secret.Length - i * 4))));
        return new TwoFactorSetupDto(grouped, uri, "data:image/png;base64," + Convert.ToBase64String(png));
    }

    public async Task<TwoFactorEnabledDto> EnableAsync(Guid adminId, EnableTwoFactorRequest request, CancellationToken ct = default)
    {
        var admin = await FindAsync(adminId, ct);
        var pending = protector.Unprotect(admin.PendingTwoFactorSecretEncrypted)
            ?? throw new ConflictException("Start the setup again: scan the new QR code first.");

        var step = Totp.Verify(pending, request.Code, clock.GetUtcNow());
        if (step is null)
        {
            throw new ConflictException("That code is not right. Check the time on your phone and try again.");
        }

        admin.TwoFactorSecretEncrypted = admin.PendingTwoFactorSecretEncrypted;
        admin.PendingTwoFactorSecretEncrypted = null;
        admin.TwoFactorEnabled = true;
        admin.TwoFactorLastStep = step.Value;
        var codes = NewRecoveryCodes(admin);
        await db.SaveChangesAsync(ct);
        return new TwoFactorEnabledDto(codes);
    }

    public async Task<TwoFactorEnabledDto> RegenerateRecoveryCodesAsync(Guid adminId, EnableTwoFactorRequest request, CancellationToken ct = default)
    {
        var admin = await FindAsync(adminId, ct);
        if (!admin.TwoFactorEnabled || !CheckCode(admin, request.Code, allowRecovery: false))
        {
            throw new ConflictException("That code is not right. Use the 6-digit code from your app.");
        }
        var codes = NewRecoveryCodes(admin);
        await db.SaveChangesAsync(ct);
        return new TwoFactorEnabledDto(codes);
    }

    public async Task DisableAsync(Guid adminId, DisableTwoFactorRequest request, CancellationToken ct = default)
    {
        var admin = await FindAsync(adminId, ct);
        if (!passwordHasher.Verify(request.Password, admin.PasswordHash))
        {
            throw new ConflictException("The password is not right.");
        }
        if (admin.TwoFactorEnabled && !CheckCode(admin, request.Code, allowRecovery: true))
        {
            throw new ConflictException("That code is not right.");
        }

        TurnOff(admin);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Server-side emergency switch (SuperAdmin:DisableTwoFactor), for a lost phone and lost recovery codes.</summary>
    public static void TurnOff(SuperAdmin admin)
    {
        admin.TwoFactorEnabled = false;
        admin.TwoFactorSecretEncrypted = null;
        admin.PendingTwoFactorSecretEncrypted = null;
        admin.RecoveryCodeHashes = null;
        admin.TwoFactorLastStep = 0;
    }

    /// <summary>A 6-digit code (each one only once), or an unused recovery code (then used up).</summary>
    private bool CheckCode(SuperAdmin admin, string? code, bool allowRecovery)
    {
        var secret = protector.Unprotect(admin.TwoFactorSecretEncrypted);
        if (secret is not null)
        {
            var step = Totp.Verify(secret, code, clock.GetUtcNow());
            if (step is not null && step.Value > admin.TwoFactorLastStep)
            {
                admin.TwoFactorLastStep = step.Value;
                return true;
            }
        }

        if (!allowRecovery)
        {
            return false;
        }

        var normalized = new string((code ?? string.Empty).ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
        if (normalized.Length != 10)
        {
            return false;
        }
        var hash = Hash(normalized);
        var hashes = RecoveryHashes(admin);
        if (!hashes.Remove(hash))
        {
            return false;
        }
        admin.RecoveryCodeHashes = string.Join(',', hashes);
        return true;
    }

    private static List<string> NewRecoveryCodes(SuperAdmin admin)
    {
        var codes = Enumerable.Range(0, RecoveryCodeCount)
            .Select(_ => RandomNumberGenerator.GetString(RecoveryAlphabet, 10))
            .ToList();
        admin.RecoveryCodeHashes = string.Join(',', codes.Select(Hash));
        // Shown as "abcde-fghij"; the dash is optional when typing.
        return codes.Select(c => $"{c[..5]}-{c[5..]}").ToList();
    }

    private static List<string> RecoveryHashes(SuperAdmin admin) =>
        string.IsNullOrEmpty(admin.RecoveryCodeHashes) ? [] : [.. admin.RecoveryCodeHashes.Split(',', StringSplitOptions.RemoveEmptyEntries)];

    private static string Hash(string code) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code))).ToLowerInvariant();

    private async Task<SuperAdmin> FindAsync(Guid adminId, CancellationToken ct) =>
        await db.SuperAdmins.FirstOrDefaultAsync(a => a.Id == adminId, ct) ?? throw new NotFoundException("Super admin not found.");
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using QrMenu.Application.Common.Interfaces;
using QrMenu.Domain.Entities;

namespace QrMenu.Infrastructure.Persistence.Seed;

/// <summary>
/// Creates the first super admin from configuration (SuperAdmin:Email / SuperAdmin:Password).
/// Keep the real values in user-secrets or appsettings.Development.json, never in source control.
/// </summary>
public static class SuperAdminSeeder
{
    public static async Task SeedAsync(AppDbContext db, IConfiguration config, IPasswordHasher passwordHasher, ILogger logger)
    {
        var email = config["SuperAdmin:Email"]?.Trim().ToLowerInvariant();
        var password = config["SuperAdmin:Password"];
        var name = string.IsNullOrWhiteSpace(config["SuperAdmin:Name"]) ? "Super Admin" : config["SuperAdmin:Name"]!.Trim();

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
        {
            if (!await db.SuperAdmins.AnyAsync())
            {
                logger.LogWarning("No super admin exists yet. Set SuperAdmin:Email and SuperAdmin:Password (user-secrets or appsettings.Development.json) and restart.");
            }

            return;
        }

        if (password.Length < 10)
        {
            logger.LogWarning("SuperAdmin:Password must be at least 10 characters. No super admin was created.");
            return;
        }

        var existing = await db.SuperAdmins.FirstOrDefaultAsync(a => a.Email == email);
        if (existing is not null)
        {
            // Lost phone and lost recovery codes: whoever controls the server's configuration can switch it off.
            if (config.GetValue("SuperAdmin:DisableTwoFactor", false) && existing.TwoFactorEnabled)
            {
                QrMenu.Infrastructure.Services.SuperAdminTwoFactorService.TurnOff(existing);
                await db.SaveChangesAsync();
                logger.LogWarning("Two-step login was turned off for {Email} (SuperAdmin:DisableTwoFactor). Remove that setting now.", email);
            }
            return;
        }

        db.SuperAdmins.Add(new SuperAdmin
        {
            Id = Guid.NewGuid(),
            Name = name,
            Email = email,
            PasswordHash = passwordHasher.Hash(password)
        });

        await db.SaveChangesAsync();
        logger.LogInformation("Super admin {Email} created.", email);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QrMenu.Application.Auth;
using QrMenu.Application.Common;
using QrMenu.Application.Common.Exceptions;
using QrMenu.Application.Common.Interfaces;
using QrMenu.Application.Platform;
using QrMenu.Application.Restaurants;
using QrMenu.Domain.Entities;
using QrMenu.Infrastructure.Auth;
using QrMenu.Infrastructure.Persistence;

namespace QrMenu.Infrastructure.Services;

public class AuthService(
    AppDbContext db,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService,
    IPlatformSettingsService platformSettings,
    IOptions<SiteSettings> siteOptions) : IAuthService
{
    // Hash of a random password, checked when the email is unknown so both cases take the same time.
    private const string DummyHash = "$2b$11$QEhrqcyGU9JYdr6rhd9/.OqiE6pNp2swEm1UYbKYSH0CIrj5rane2";

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var email = request.Email.NormalizeEmail();
        var emailExists = await db.Users.AnyAsync(u => u.Email == email, ct);
        if (emailExists)
        {
            throw new ConflictException("An account with this email already exists.");
        }

        var slug = await GenerateUniqueSlugAsync(request.RestaurantName, ct);
        // Set by the super admin under Settings (falls back to Subscription:TrialDays in configuration).
        var trialDays = await platformSettings.GetTrialDaysAsync(ct);
        var now = DateTime.UtcNow;

        var restaurant = new Restaurant
        {
            Id = Guid.NewGuid(),
            Name = request.RestaurantName,
            Slug = slug,
            Subdomain = await PickSubdomainAsync(slug, ct),
            WhatsAppNumber = request.WhatsAppNumber,
            OpenTime = new TimeSpan(9, 0, 0),
            CloseTime = new TimeSpan(23, 0, 0),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            // New restaurants start on a free trial, then must buy a plan. 0 days = no trial: the trial ends
            // straight away, so the owner can set up the menu but customers cannot order until a plan is bought.
            // New restaurants print their QR codes with the secret table code, so only table QR orders from day one.
            RequireTableQr = true,
            AllowLinkTakeaway = false,
            Plan = SubscriptionPlan.Trial,
            PlanName = "Trial",
            PlanExpiresAt = now.AddDays(Math.Max(0, trialDays))
        };

        db.SubscriptionEvents.Add(new SubscriptionEvent
        {
            Id = Guid.NewGuid(),
            RestaurantId = restaurant.Id,
            Action = SubscriptionAction.TrialStarted,
            Plan = restaurant.Plan,
            PlanName = restaurant.PlanName,
            ExpiresAt = restaurant.PlanExpiresAt,
            PerformedBy = "system",
            CreatedAt = DateTime.UtcNow
        });

        var user = new User
        {
            Id = Guid.NewGuid(),
            RestaurantId = restaurant.Id,
            Name = request.OwnerName,
            Email = email,
            PasswordHash = passwordHasher.Hash(request.Password),
            CreatedAt = DateTime.UtcNow
        };

        db.Restaurants.Add(restaurant);
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        var token = jwtTokenService.GenerateToken(user);
        return new AuthResponse(token, user.Name, restaurant.Id, restaurant.Slug, restaurant.Name);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var email = request.Email.NormalizeEmail();
        var lockKey = $"owner:{email}";
        if (LoginAttemptTracker.IsLocked(lockKey))
        {
            throw new TooManyAttemptsException("Too many failed attempts. Please try again in a few minutes.");
        }

        var user = await db.Users.Include(u => u.Restaurant)
            .FirstOrDefaultAsync(u => u.Email == email, ct);
        var passwordOk = passwordHasher.Verify(request.Password, user?.PasswordHash ?? DummyHash);

        if (user is null || !passwordOk)
        {
            LoginAttemptTracker.RecordFailure(lockKey);
            throw new UnauthorizedAppException("Invalid email or password.");
        }

        LoginAttemptTracker.Reset(lockKey);

        if (!user.Restaurant.IsActive)
        {
            throw new ForbiddenException("This restaurant account is suspended. Please contact support.", "restaurant_suspended");
        }

        var token = jwtTokenService.GenerateToken(user);
        return new AuthResponse(token, user.Name, user.RestaurantId, user.Restaurant.Slug, user.Restaurant.Name);
    }

    public async Task<AuthResponse> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct = default)
    {
        var user = await db.Users.Include(u => u.Restaurant).FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new NotFoundException("Account not found.");

        var lockKey = $"owner-change:{user.Id}";
        if (LoginAttemptTracker.IsLocked(lockKey))
        {
            throw new TooManyAttemptsException("Too many failed attempts. Please try again in a few minutes.");
        }

        if (!passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            LoginAttemptTracker.RecordFailure(lockKey);
            throw new ConflictException("The current password is not correct.");
        }

        LoginAttemptTracker.Reset(lockKey);
        user.PasswordHash = passwordHasher.Hash(request.NewPassword);
        user.PasswordVersion++;
        await db.SaveChangesAsync(ct);

        var token = jwtTokenService.GenerateToken(user);
        return new AuthResponse(token, user.Name, user.RestaurantId, user.Restaurant.Slug, user.Restaurant.Name);
    }

    /// <summary>The slug as the restaurant's own address when it is allowed and still free, otherwise none.</summary>
    private async Task<string?> PickSubdomainAsync(string slug, CancellationToken ct)
    {
        if (!siteOptions.Value.SubdomainsEnabled || SubdomainRules.Problem(slug) is not null)
        {
            return null;
        }

        return await db.Restaurants.AnyAsync(r => r.Subdomain == slug, ct) ? null : slug;
    }

    private async Task<string> GenerateUniqueSlugAsync(string restaurantName, CancellationToken ct)
    {
        var baseSlug = Slugify(restaurantName);
        var slug = baseSlug;
        var suffix = 1;

        while (await db.Restaurants.AnyAsync(r => r.Slug == slug, ct))
        {
            suffix++;
            slug = $"{baseSlug}-{suffix}";
        }

        return slug;
    }

    private static string Slugify(string input)
    {
        var lowered = input.Trim().ToLowerInvariant();
        var chars = lowered.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray();
        var slug = new string(chars);
        while (slug.Contains("--"))
        {
            slug = slug.Replace("--", "-");
        }
        return slug.Trim('-') is { Length: > 0 } trimmed ? trimmed : Guid.NewGuid().ToString("N")[..8];
    }
}

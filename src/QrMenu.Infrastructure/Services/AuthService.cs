using Microsoft.EntityFrameworkCore;
using QrMenu.Application.Auth;
using QrMenu.Application.Common.Exceptions;
using QrMenu.Application.Common.Interfaces;
using QrMenu.Application.Platform;
using QrMenu.Domain.Entities;
using QrMenu.Infrastructure.Persistence;

namespace QrMenu.Infrastructure.Services;

public class AuthService(
    AppDbContext db,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService,
    IPlatformSettingsService platformSettings) : IAuthService
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var emailExists = await db.Users.AnyAsync(u => u.Email == request.Email, ct);
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
            WhatsAppNumber = request.WhatsAppNumber,
            OpenTime = new TimeSpan(9, 0, 0),
            CloseTime = new TimeSpan(23, 0, 0),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            // New restaurants start on a free trial, then must buy a plan. 0 days = no trial: the trial ends
            // straight away, so the owner can set up the menu but customers cannot order until a plan is bought.
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
            Email = request.Email,
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
        var user = await db.Users.Include(u => u.Restaurant)
            .FirstOrDefaultAsync(u => u.Email == request.Email, ct);

        if (user is null || !passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedAppException("Invalid email or password.");
        }

        if (!user.Restaurant.IsActive)
        {
            throw new ForbiddenException("This restaurant account is suspended. Please contact support.", "restaurant_suspended");
        }

        var token = jwtTokenService.GenerateToken(user);
        return new AuthResponse(token, user.Name, user.RestaurantId, user.Restaurant.Slug, user.Restaurant.Name);
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

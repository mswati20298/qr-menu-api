using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QrMenu.Application.Auth;
using QrMenu.Application.Common.Exceptions;
using QrMenu.Application.Common.Interfaces;
using QrMenu.Application.Subscriptions;
using QrMenu.Domain.Entities;
using QrMenu.Infrastructure.Persistence;

namespace QrMenu.Infrastructure.Services;

public class AuthService(
    AppDbContext db,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService,
    IOptions<SubscriptionSettings> subscriptionOptions) : IAuthService
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var emailExists = await db.Users.AnyAsync(u => u.Email == request.Email, ct);
        if (emailExists)
        {
            throw new ConflictException("An account with this email already exists.");
        }

        var slug = await GenerateUniqueSlugAsync(request.RestaurantName, ct);
        var trialDays = subscriptionOptions.Value.TrialDays;

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
            // New restaurants start on a free trial, then must buy a plan; TrialDays <= 0 means free with no end date.
            Plan = trialDays > 0 ? SubscriptionPlan.Trial : SubscriptionPlan.Free,
            PlanName = trialDays > 0 ? "Trial" : "Free",
            PlanExpiresAt = trialDays > 0 ? DateTime.UtcNow.AddDays(trialDays) : null
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

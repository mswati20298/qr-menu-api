using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QrMenu.Application.Common.Exceptions;
using QrMenu.Application.Common.Interfaces;
using QrMenu.Application.Subscriptions;
using QrMenu.Application.SuperAdmins;
using QrMenu.Domain.Entities;
using QrMenu.Infrastructure.Auth;
using QrMenu.Infrastructure.Persistence;

namespace QrMenu.Infrastructure.Services;

public class SuperAdminService(
    AppDbContext db,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService,
    IOptions<SubscriptionSettings> subscriptionOptions) : ISuperAdminService
{
    // Hash of a random password. Checked when the email is unknown so a wrong email and a wrong
    // password take the same time (no way to tell which super admin emails exist).
    private const string DummyHash = "$2b$11$QEhrqcyGU9JYdr6rhd9/.OqiE6pNp2swEm1UYbKYSH0CIrj5rane2";

    public async Task<SuperAdminAuthResponse> LoginAsync(SuperAdminLoginRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        if (LoginAttemptTracker.IsLocked(email))
        {
            throw new TooManyAttemptsException("Too many failed attempts. Please try again in a few minutes.");
        }

        var admin = await db.SuperAdmins.FirstOrDefaultAsync(a => a.Email == email, ct);
        var passwordOk = passwordHasher.Verify(request.Password, admin?.PasswordHash ?? DummyHash);

        if (admin is null || !passwordOk)
        {
            LoginAttemptTracker.RecordFailure(email);
            throw new UnauthorizedAppException("Invalid email or password.");
        }

        LoginAttemptTracker.Reset(email);
        return new SuperAdminAuthResponse(jwtTokenService.GenerateSuperAdminToken(admin), admin.Name);
    }

    public async Task<ResetOwnerPasswordResponse> ResetOwnerPasswordAsync(Guid restaurantId, CancellationToken ct = default)
    {
        var owner = await db.Users
            .Where(u => u.RestaurantId == restaurantId)
            .OrderBy(u => u.CreatedAt)
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Restaurant owner not found.");

        // No look-alike characters (0/O, 1/l/I), so it can be read out over the phone.
        const string alphabet = "abcdefghjkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var temporary = RandomNumberGenerator.GetString(alphabet, 10);

        owner.PasswordHash = passwordHasher.Hash(temporary);
        owner.PasswordVersion++;
        await db.SaveChangesAsync(ct);
        LoginAttemptTracker.Reset($"owner:{owner.Email.ToLowerInvariant()}");

        return new ResetOwnerPasswordResponse(owner.Email, temporary);
    }

    public async Task<SuperAdminStatsDto> GetStatsAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var since = now.AddDays(-30);

        var total = await db.Restaurants.CountAsync(ct);
        var active = await db.Restaurants.CountAsync(r => r.IsActive, ct);
        var recent = await db.Restaurants.CountAsync(r => r.CreatedAt >= since, ct);
        var orders = await db.Orders.CountAsync(ct);
        var recentOrders = await db.Orders.CountAsync(o => o.CreatedAt >= since, ct);

        var trial = await FilterByPlan(db.Restaurants, "trial", now).CountAsync(ct);
        var paid = await FilterByPlan(db.Restaurants, "paid", now).CountAsync(ct);
        var expiring = await FilterByPlan(db.Restaurants, "expiring", now).CountAsync(ct);
        var grace = await FilterByPlan(db.Restaurants, "grace", now).CountAsync(ct);
        var stopped = await FilterByPlan(db.Restaurants, "stopped", now).CountAsync(ct);

        return new SuperAdminStatsDto(total, active, total - active, recent, orders, recentOrders, trial, paid, expiring, grace, stopped);
    }

    public async Task<PagedResult<SuperAdminRestaurantDto>> ListRestaurantsAsync(
        string? search, string? status, string? plan, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.Restaurants.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(r =>
                r.Name.Contains(term)
                || r.Slug.Contains(term)
                || db.Users.Any(u => u.RestaurantId == r.Id && (u.Email.Contains(term) || u.Name.Contains(term))));
        }

        if (status == "active")
        {
            query = query.Where(r => r.IsActive);
        }
        else if (status == "suspended")
        {
            query = query.Where(r => !r.IsActive);
        }

        query = FilterByPlan(query, plan, DateTime.UtcNow);

        var total = await query.CountAsync(ct);

        var items = await Project(query.OrderByDescending(r => r.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize))
            .ToListAsync(ct);

        return new PagedResult<SuperAdminRestaurantDto>(items, total, page, pageSize);
    }

    public async Task<SuperAdminRestaurantDto> SetRestaurantStatusAsync(Guid restaurantId, bool isActive, CancellationToken ct = default)
    {
        var restaurant = await db.Restaurants.FirstOrDefaultAsync(r => r.Id == restaurantId, ct)
            ?? throw new NotFoundException("Restaurant not found.");

        restaurant.IsActive = isActive;
        await db.SaveChangesAsync(ct);

        return await Project(db.Restaurants.AsNoTracking().Where(r => r.Id == restaurantId)).FirstAsync(ct);
    }

    /// <summary>Same rules as SubscriptionRules.GetStatus, written so SQL Server can run them.</summary>
    private IQueryable<Restaurant> FilterByPlan(IQueryable<Restaurant> query, string? plan, DateTime now)
    {
        var settings = subscriptionOptions.Value;
        var graceCutoff = now.AddDays(-settings.GraceDays);
        var soon = now.AddDays(settings.ExpiringSoonDays);

        // Only paid plans get a grace period; a trial or free plan stops on its end date.
        return plan switch
        {
            "trial" => query.Where(r => r.Plan == SubscriptionPlan.Trial),
            "free" => query.Where(r => r.Plan == SubscriptionPlan.Free),
            "paid" => query.Where(r => r.Plan == SubscriptionPlan.Paid && r.PlanCancelledAt == null
                && r.PlanExpiresAt > graceCutoff),
            "expiring" => query.Where(r => r.PlanCancelledAt == null && r.PlanExpiresAt > now && r.PlanExpiresAt <= soon),
            "grace" => query.Where(r => r.Plan == SubscriptionPlan.Paid && r.PlanCancelledAt == null
                && r.PlanExpiresAt <= now && r.PlanExpiresAt > graceCutoff),
            "stopped" => query.Where(r => r.PlanCancelledAt != null
                || (r.Plan == SubscriptionPlan.Paid ? r.PlanExpiresAt <= graceCutoff : r.PlanExpiresAt <= now)),
            _ => query
        };
    }

    private IQueryable<SuperAdminRestaurantDto> Project(IQueryable<Restaurant> query)
    {
        var graceDays = subscriptionOptions.Value.GraceDays;
        var now = DateTime.UtcNow;

        return query.Select(r => new SuperAdminRestaurantDto(
            r.Id,
            r.Name,
            r.Slug,
            db.Users.Where(u => u.RestaurantId == r.Id).OrderBy(u => u.CreatedAt).Select(u => u.Name).FirstOrDefault() ?? "",
            db.Users.Where(u => u.RestaurantId == r.Id).OrderBy(u => u.CreatedAt).Select(u => u.Email).FirstOrDefault() ?? "",
            r.WhatsAppNumber,
            r.IsActive,
            r.CreatedAt,
            db.Orders.Count(o => o.RestaurantId == r.Id),
            db.Orders.Where(o => o.RestaurantId == r.Id).Max(o => (DateTime?)o.CreatedAt),
            r.Plan.ToString(),
            SubscriptionRules.DisplayName(r.Plan, r.PlanName),
            SubscriptionRules.GetStatus(r.Plan, r.PlanExpiresAt, r.PlanCancelledAt, graceDays, now),
            r.PlanExpiresAt));
    }
}

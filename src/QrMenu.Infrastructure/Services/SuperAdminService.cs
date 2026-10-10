using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QrMenu.Application.Common;
using QrMenu.Application.Common.Exceptions;
using QrMenu.Application.Common.Interfaces;
using QrMenu.Application.Subscriptions;
using QrMenu.Application.Refunds;
using QrMenu.Application.SuperAdmins;
using QrMenu.Domain.Entities;
using QrMenu.Infrastructure.Auth;
using QrMenu.Infrastructure.Persistence;

namespace QrMenu.Infrastructure.Services;

public class SuperAdminService(
    AppDbContext db,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService,
    IOptions<SubscriptionSettings> subscriptionOptions,
    TwoFactorChallenges twoFactorChallenges) : ISuperAdminService
{
    // Hash of a random password. Checked when the email is unknown so a wrong email and a wrong
    // password take the same time (no way to tell which super admin emails exist).
    private const string DummyHash = "$2b$11$QEhrqcyGU9JYdr6rhd9/.OqiE6pNp2swEm1UYbKYSH0CIrj5rane2";

    public async Task<SuperAdminAuthResponse> LoginAsync(SuperAdminLoginRequest request, CancellationToken ct = default)
    {
        var email = request.Email.NormalizeEmail();

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

        // Two-step login: no token yet, only a 5-minute challenge for the code step.
        if (admin.TwoFactorEnabled)
        {
            return new SuperAdminAuthResponse(string.Empty, admin.Name, RequiresTwoFactor: true, ChallengeToken: twoFactorChallenges.Issue(admin.Id));
        }

        return new SuperAdminAuthResponse(jwtTokenService.GenerateSuperAdminToken(admin), admin.Name);
    }

    public async Task<List<PaymentGatewayLogDto>> GetGatewayLogAsync(string gatewayOrderId, CancellationToken ct = default)
    {
        // Refund webhooks name the payment, not the order: include them too.
        var paymentId = await db.PlanPayments.AsNoTracking()
            .Where(p => p.GatewayOrderId == gatewayOrderId)
            .Select(p => p.GatewayPaymentId)
            .FirstOrDefaultAsync(ct);
        var paymentKey = paymentId is null ? null : "payment:" + paymentId;

        return await db.PaymentGatewayLogs.AsNoTracking()
            .Where(l => l.GatewayOrderId == gatewayOrderId || (paymentKey != null && l.GatewayOrderId == paymentKey))
            .OrderBy(l => l.CreatedAt)
            .Select(l => new PaymentGatewayLogDto(l.Kind, l.StatusCode, l.RequestBody, l.ResponseBody, l.Note, l.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<PaymentLogResponse> ListPaymentsAsync(string? status, string? search, CancellationToken ct = default)
    {
        var term = search?.Trim();

        // Razorpay checkouts: each one is a row from the moment the owner presses Pay.
        var online = await db.PlanPayments.AsNoTracking()
            .Where(p => string.IsNullOrEmpty(term) || p.Restaurant.Name.Contains(term) || p.GatewayOrderId.Contains(term)
                || (p.GatewayPaymentId != null && p.GatewayPaymentId.Contains(term)))
            .OrderByDescending(p => p.CreatedAt)
            .Take(500)
            .Select(p => new PaymentLogDto(
                p.Id, "online", p.Status == PlanPaymentStatus.Paid ? "Paid" : "Not completed",
                p.RestaurantId, p.Restaurant.Name, p.PlanName, p.Amount, "Razorpay",
                p.GatewayOrderId, p.GatewayPaymentId, null, null, "owner (online)", p.CreatedAt, p.PaidAt, 0, null,
                p.GatewayFee ?? Math.Round(p.Amount * RefundRules.EstimatedFeePercent / 100, 2)))
            .ToListAsync(ct);

        // Recorded by hand in the panel. (Online payments also leave a PaymentRecorded event; skip those.)
        var manual = await db.SubscriptionEvents.AsNoTracking()
            .Where(e => e.Action == SubscriptionAction.PaymentRecorded && e.PaymentMethod != PaymentMethod.Online)
            .Where(e => string.IsNullOrEmpty(term) || e.Restaurant.Name.Contains(term) || (e.PaymentReference != null && e.PaymentReference.Contains(term)))
            .OrderByDescending(e => e.CreatedAt)
            .Take(500)
            .Select(e => new PaymentLogDto(
                e.Id, "manual", "Paid", e.RestaurantId, e.Restaurant.Name, e.PlanName, e.Amount ?? 0,
                e.PaymentMethod.HasValue ? e.PaymentMethod.Value.ToString() : null,
                null, null, e.PaymentReference, e.Note, e.PerformedBy, e.CreatedAt, e.CreatedAt, 0, null, 0))
            .ToListAsync(ct);

        // Refunds per payment (money on its way or gone), plus the latest refund state to show on the row.
        var refunds = await db.PlanRefunds.AsNoTracking()
            .Select(r => new { r.PlanPaymentId, r.PaymentEventId, r.Amount, r.Status, r.RequestedAt })
            .ToListAsync(ct);
        string Key(Guid? planPaymentId, Guid? eventId) => planPaymentId?.ToString() ?? eventId?.ToString() ?? "";
        var byPayment = refunds.GroupBy(r => Key(r.PlanPaymentId, r.PaymentEventId)).ToDictionary(g => g.Key, g => g.ToList());

        PaymentLogDto WithRefunds(PaymentLogDto p)
        {
            if (!byPayment.TryGetValue(p.Id.ToString(), out var list))
            {
                return p;
            }
            var refunded = list.Where(r => r.Status is RefundStatus.Processing or RefundStatus.Refunded).Sum(r => r.Amount);
            var latest = list.OrderByDescending(r => r.RequestedAt).First().Status.ToString();
            return p with { RefundedAmount = refunded, RefundStatus = latest };
        }

        var all = online.Concat(manual).Select(WithRefunds).OrderByDescending(p => p.CreatedAt).ToList();
        var summary = new PaymentLogSummaryDto(
            all.Where(p => p.Status == "Paid").Sum(p => p.Amount),
            all.Count(p => p.Status == "Paid"),
            all.Count(p => p.Status != "Paid"),
            refunds.Where(r => r.Status is RefundStatus.Processing or RefundStatus.Refunded).Sum(r => r.Amount),
            refunds.Count(r => r.Status == RefundStatus.Requested));

        var items = status?.ToLowerInvariant() switch
        {
            "paid" => all.Where(p => p.Status == "Paid"),
            "unpaid" => all.Where(p => p.Status != "Paid"),
            _ => all
        };
        return new PaymentLogResponse(summary, items.Take(500).ToList());
    }

    public async Task<SuperAdminAuthResponse> ChangePasswordAsync(Guid adminId, ChangeSuperAdminPasswordRequest request, CancellationToken ct = default)
    {
        var lockKey = $"superadmin-change:{adminId:N}";
        if (LoginAttemptTracker.IsLocked(lockKey))
        {
            throw new TooManyAttemptsException("Too many wrong passwords. Please try again in a few minutes.");
        }

        var admin = await db.SuperAdmins.FirstOrDefaultAsync(a => a.Id == adminId, ct)
            ?? throw new NotFoundException("Super admin not found.");
        if (!passwordHasher.Verify(request.CurrentPassword, admin.PasswordHash))
        {
            LoginAttemptTracker.RecordFailure(lockKey);
            throw new ConflictException("The current password is not right.");
        }

        LoginAttemptTracker.Reset(lockKey);
        admin.PasswordHash = passwordHasher.Hash(request.NewPassword);
        admin.PasswordVersion++;
        await db.SaveChangesAsync(ct);
        return new SuperAdminAuthResponse(jwtTokenService.GenerateSuperAdminToken(admin), admin.Name);
    }

    public async Task<int?> GetPasswordVersionAsync(Guid adminId, CancellationToken ct = default) =>
        await db.SuperAdmins.AsNoTracking().Where(a => a.Id == adminId).Select(a => (int?)a.PasswordVersion).FirstOrDefaultAsync(ct);

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

        var live = db.Restaurants.Where(r => r.DeletedAt == null);
        var total = await live.CountAsync(ct);
        var active = await live.CountAsync(r => r.IsActive, ct);
        var recent = await live.CountAsync(r => r.CreatedAt >= since, ct);
        var orders = await db.Orders.CountAsync(ct);
        var recentOrders = await db.Orders.CountAsync(o => o.CreatedAt >= since, ct);

        var trial = await FilterByPlan(live, "trial", now).CountAsync(ct);
        var paid = await FilterByPlan(live, "paid", now).CountAsync(ct);
        var expiring = await FilterByPlan(live, "expiring", now).CountAsync(ct);
        var grace = await FilterByPlan(live, "grace", now).CountAsync(ct);
        var stopped = await FilterByPlan(live, "stopped", now).CountAsync(ct);

        // --- Money and activity, counted in Indian calendar days ---
        var today = IndianTime.DateOf(now);
        var firstDay = today.AddDays(-29);
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var firstMonth = monthStart.AddMonths(-11);
        var since12Months = IndianTime.StartOfDayUtc(firstMonth);
        var since30Days = IndianTime.StartOfDayUtc(firstDay);

        // Refunds are money going back: they count as negative amounts on the day they were made.
        var payments = await db.SubscriptionEvents.AsNoTracking()
            .Where(e => (e.Action == SubscriptionAction.PaymentRecorded || e.Action == SubscriptionAction.Refunded) && e.Amount != null && e.Amount > 0)
            .Select(e => new
            {
                e.RestaurantId,
                e.PlanName,
                Amount = e.Action == SubscriptionAction.Refunded ? -e.Amount!.Value : e.Amount!.Value,
                e.PaymentMethod,
                e.CreatedAt
            })
            .ToListAsync(ct);
        var allTime = payments.Sum(p => p.Amount);
        var paymentsByDay = payments.Where(p => p.CreatedAt >= since12Months).GroupBy(p => IndianTime.DateOf(p.CreatedAt))
            .ToDictionary(g => g.Key, g => (Revenue: g.Sum(p => p.Amount), Count: g.Count(p => p.Amount > 0)));

        var signupsByDay = (await db.Restaurants.AsNoTracking().Where(r => r.CreatedAt >= since30Days).Select(r => r.CreatedAt).ToListAsync(ct))
            .GroupBy(IndianTime.DateOf).ToDictionary(g => g.Key, g => g.Count());

        var orders30 = await db.Orders.AsNoTracking().Where(o => o.CreatedAt >= since30Days)
            .Select(o => new { o.RestaurantId, o.CreatedAt }).ToListAsync(ct);
        var ordersByDay = orders30.GroupBy(o => IndianTime.DateOf(o.CreatedAt)).ToDictionary(g => g.Key, g => g.ToList());

        (decimal Revenue, int Count) PaidOn(DateOnly day) => paymentsByDay.GetValueOrDefault(day);
        int OrdersOn(DateOnly day) => ordersByDay.GetValueOrDefault(day)?.Count ?? 0;

        var last30 = Enumerable.Range(0, 30).Select(i => firstDay.AddDays(i))
            .Select(d => new PlatformDayDto(d, PaidOn(d).Revenue, PaidOn(d).Count, signupsByDay.GetValueOrDefault(d), OrdersOn(d)))
            .ToList();

        var last12 = Enumerable.Range(0, 12).Select(i => firstMonth.AddMonths(i))
            .Select(m =>
            {
                var inMonth = paymentsByDay.Where(kv => kv.Key.Year == m.Year && kv.Key.Month == m.Month).Select(kv => kv.Value).ToList();
                return new PlatformMonthDto($"{m:yyyy-MM}", inMonth.Sum(v => v.Revenue), inMonth.Sum(v => v.Count));
            })
            .ToList();

        var names = await db.Restaurants.AsNoTracking()
            .Select(r => new { r.Id, r.Name, r.LogoUrl })
            .ToDictionaryAsync(r => r.Id, ct);

        var recentPayments = payments.OrderByDescending(p => p.CreatedAt).Take(6)
            .Where(p => names.ContainsKey(p.RestaurantId))
            .Select(p => new RecentPaymentDto(p.RestaurantId, names[p.RestaurantId].Name, names[p.RestaurantId].LogoUrl,
                p.PlanName, p.Amount, p.PaymentMethod?.ToString(), p.CreatedAt))
            .ToList();

        var soon = now.AddDays(7);
        var renewals = await db.Restaurants.AsNoTracking()
            .Where(r => r.IsActive && r.PlanCancelledAt == null && r.PlanExpiresAt != null && r.PlanExpiresAt > now && r.PlanExpiresAt <= soon)
            .OrderBy(r => r.PlanExpiresAt)
            .Take(6)
            .Select(r => new RenewalDueDto(r.Id, r.Name, r.LogoUrl, r.PlanName, r.Plan.ToString(), r.PlanExpiresAt!.Value))
            .ToListAsync(ct);

        var topIds = orders30.GroupBy(o => o.RestaurantId)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count).Take(5).ToList();
        var topPlans = await db.Restaurants.AsNoTracking()
            .Where(r => topIds.Select(t => t.Id).Contains(r.Id))
            .Select(r => new { r.Id, Plan = r.Plan.ToString() })
            .ToDictionaryAsync(r => r.Id, r => r.Plan, ct);
        var topRestaurants = topIds.Where(t => names.ContainsKey(t.Id))
            .Select(t => new TopRestaurantDto(t.Id, names[t.Id].Name, names[t.Id].LogoUrl, topPlans.GetValueOrDefault(t.Id, ""), t.Count))
            .ToList();

        var thisMonth = last12[^1];
        var lastMonth = last12[^2];

        return new SuperAdminStatsDto(total, active, total - active, recent, orders, recentOrders, trial, paid, expiring, grace, stopped,
            PaidOn(today).Revenue, PaidOn(today.AddDays(-1)).Revenue, thisMonth.Revenue, lastMonth.Revenue, allTime, thisMonth.Payments,
            OrdersOn(today), OrdersOn(today.AddDays(-1)),
            ordersByDay.GetValueOrDefault(today)?.Select(o => o.RestaurantId).Distinct().Count() ?? 0,
            last30, last12, recentPayments, renewals, topRestaurants);
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

        if (status == "deleted")
        {
            query = query.Where(r => r.DeletedAt != null);
        }
        else
        {
            query = query.Where(r => r.DeletedAt == null);
            if (status == "active")
            {
                query = query.Where(r => r.IsActive);
            }
            else if (status == "suspended")
            {
                query = query.Where(r => !r.IsActive);
            }
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
        if (restaurant.DeletedAt is not null)
        {
            throw new ConflictException("This restaurant is deleted. Restore it first.");
        }

        restaurant.IsActive = isActive;
        await db.SaveChangesAsync(ct);

        return await Project(db.Restaurants.AsNoTracking().Where(r => r.Id == restaurantId)).FirstAsync(ct);
    }

    public async Task<SuperAdminRestaurantDto> SoftDeleteRestaurantAsync(Guid restaurantId, string deletedBy, CancellationToken ct = default)
    {
        var restaurant = await db.Restaurants.FirstOrDefaultAsync(r => r.Id == restaurantId, ct)
            ?? throw new NotFoundException("Restaurant not found.");
        if (restaurant.DeletedAt is null)
        {
            restaurant.DeletedAt = DateTime.UtcNow;
            restaurant.DeletedBy = deletedBy;
            // Same effect as a suspension: owner and kitchen are signed out, the menu goes offline.
            restaurant.IsActive = false;
            await db.SaveChangesAsync(ct);
        }
        return await Project(db.Restaurants.AsNoTracking().Where(r => r.Id == restaurantId)).FirstAsync(ct);
    }

    public async Task<SuperAdminRestaurantDto> RestoreRestaurantAsync(Guid restaurantId, CancellationToken ct = default)
    {
        var restaurant = await db.Restaurants.FirstOrDefaultAsync(r => r.Id == restaurantId, ct)
            ?? throw new NotFoundException("Restaurant not found.");
        if (restaurant.DeletedAt is not null)
        {
            restaurant.DeletedAt = null;
            restaurant.DeletedBy = null;
            restaurant.IsActive = true;
            await db.SaveChangesAsync(ct);
        }
        return await Project(db.Restaurants.AsNoTracking().Where(r => r.Id == restaurantId)).FirstAsync(ct);
    }

    public async Task HardDeleteRestaurantAsync(Guid restaurantId, string confirmName, CancellationToken ct = default)
    {
        var restaurant = await db.Restaurants.FirstOrDefaultAsync(r => r.Id == restaurantId, ct)
            ?? throw new NotFoundException("Restaurant not found.");
        if (restaurant.DeletedAt is null)
        {
            throw new ConflictException("Delete it first (soft delete). It can be removed for good only after that.");
        }
        if (!string.Equals(confirmName?.Trim(), restaurant.Name.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictException("The name you typed does not match the restaurant's name.");
        }

        // Menu, orders, bills, tables, users, payments, refunds and reviews go with it (cascade in the database).
        // A big restaurant has a lot to remove: allow more than the default 30 s, and finish even if the browser
        // goes away, so a delete is never cut off half way.
        db.Restaurants.Remove(restaurant);
        if (db.Database.IsRelational())
        {
            db.Database.SetCommandTimeout(TimeSpan.FromMinutes(2));
        }
        await db.SaveChangesAsync(CancellationToken.None);
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
            r.PlanExpiresAt,
            r.DeletedAt));
    }
}

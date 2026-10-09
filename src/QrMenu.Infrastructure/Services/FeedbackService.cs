using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QrMenu.Application.Common;
using QrMenu.Application.Common.Exceptions;
using QrMenu.Application.Feedbacks;
using QrMenu.Domain.Entities;
using QrMenu.Infrastructure.Persistence;

namespace QrMenu.Infrastructure.Services;

public class FeedbackService(AppDbContext db, IOptions<SiteSettings> siteOptions, TimeProvider clock) : IFeedbackService
{
    private const int LandingLimit = 12;

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    // ---------- Guests ----------

    public async Task<FeedbackDto?> GetForOrderAsync(string slug, Guid orderId, CancellationToken ct = default)
    {
        var (order, restaurant) = await FindOrderAsync(slug, orderId, ct);
        var feedback = await db.Feedback.AsNoTracking().FirstOrDefaultAsync(f => f.OrderId == order.Id, ct);
        return feedback is null ? null : ToDto(feedback, restaurant.Name, restaurant.LogoUrl, order.TableNumberSnapshot);
    }

    public async Task<FeedbackDto> SubmitForOrderAsync(string slug, Guid orderId, SubmitCustomerFeedbackRequest request, CancellationToken ct = default)
    {
        var (order, restaurant) = await FindOrderAsync(slug, orderId, ct);
        var name = Clean(request.Name) ?? Clean(order.CustomerName) ?? "Guest";

        // Editing is allowed: the same phone can change its rating; it stays one rating per order.
        var feedback = await db.Feedback.FirstOrDefaultAsync(f => f.OrderId == order.Id, ct);
        if (feedback is null)
        {
            feedback = new Feedback
            {
                Id = Guid.NewGuid(),
                RestaurantId = restaurant.Id,
                Kind = FeedbackKind.Customer,
                OrderId = order.Id,
                CreatedAt = Now
            };
            db.Feedback.Add(feedback);
        }

        feedback.Rating = request.Rating;
        feedback.Name = name;
        feedback.Comment = Clean(request.Comment);
        feedback.ImageUrl = Clean(request.ImageUrl);
        feedback.UpdatedAt = Now;
        await db.SaveChangesAsync(ct);

        return ToDto(feedback, restaurant.Name, restaurant.LogoUrl, order.TableNumberSnapshot);
    }

    public async Task EnsureOrderExistsAsync(string slug, Guid orderId, CancellationToken ct = default) =>
        await FindOrderAsync(slug, orderId, ct);

    // ---------- Restaurant owner ----------

    public async Task<CustomerFeedbackSummaryDto> GetCustomerFeedbackAsync(Guid restaurantId, CancellationToken ct = default)
    {
        var restaurant = await db.Restaurants.AsNoTracking().FirstAsync(r => r.Id == restaurantId, ct);
        var all = await db.Feedback.AsNoTracking()
            .Where(f => f.RestaurantId == restaurantId && f.Kind == FeedbackKind.Customer)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync(ct);

        var orderIds = all.Take(100).Where(f => f.OrderId != null).Select(f => f.OrderId!.Value).ToList();
        var tables = await db.Orders.AsNoTracking()
            .Where(o => orderIds.Contains(o.Id))
            .Select(o => new { o.Id, o.TableNumberSnapshot })
            .ToDictionaryAsync(o => o.Id, o => o.TableNumberSnapshot, ct);

        var byStars = Enumerable.Range(1, 5).Select(star => all.Count(f => f.Rating == star)).ToList();
        var average = all.Count == 0 ? 0 : Math.Round(all.Average(f => f.Rating), 1);
        var items = all.Take(100)
            .Select(f => ToDto(f, restaurant.Name, restaurant.LogoUrl, f.OrderId is { } id ? tables.GetValueOrDefault(id) : null))
            .ToList();

        return new CustomerFeedbackSummaryDto(average, all.Count, byStars, items);
    }

    public async Task<FeedbackDto?> GetOwnerFeedbackAsync(Guid restaurantId, CancellationToken ct = default)
    {
        var restaurant = await db.Restaurants.AsNoTracking().FirstAsync(r => r.Id == restaurantId, ct);
        var feedback = await db.Feedback.AsNoTracking()
            .FirstOrDefaultAsync(f => f.RestaurantId == restaurantId && f.Kind == FeedbackKind.Owner, ct);
        return feedback is null ? null : ToDto(feedback, restaurant.Name, restaurant.LogoUrl, null);
    }

    public async Task<FeedbackDto> SaveOwnerFeedbackAsync(Guid restaurantId, SubmitOwnerFeedbackRequest request, CancellationToken ct = default)
    {
        var restaurant = await db.Restaurants.AsNoTracking().FirstAsync(r => r.Id == restaurantId, ct);
        var feedback = await db.Feedback.FirstOrDefaultAsync(f => f.RestaurantId == restaurantId && f.Kind == FeedbackKind.Owner, ct);
        var changed = feedback is null
            || feedback.Rating != request.Rating
            || feedback.Comment != Clean(request.Comment)
            || feedback.Name != request.Name.Trim()
            || feedback.ImageUrl != Clean(request.ImageUrl);

        if (feedback is null)
        {
            feedback = new Feedback { Id = Guid.NewGuid(), RestaurantId = restaurantId, Kind = FeedbackKind.Owner, CreatedAt = Now };
            db.Feedback.Add(feedback);
        }

        feedback.Rating = request.Rating;
        feedback.Name = request.Name.Trim();
        feedback.Comment = Clean(request.Comment);
        feedback.ImageUrl = Clean(request.ImageUrl);
        feedback.UpdatedAt = Now;

        // An edited testimonial goes back to review, so the landing page never shows words the super admin has not seen.
        if (changed)
        {
            feedback.IsPublished = false;
        }

        await db.SaveChangesAsync(ct);
        return ToDto(feedback, restaurant.Name, restaurant.LogoUrl, null);
    }

    // ---------- Super admin ----------

    public async Task<List<FeedbackDto>> ListAllAsync(string? kind, CancellationToken ct = default)
    {
        var query = db.Feedback.AsNoTracking().AsQueryable();
        if (string.Equals(kind, "owner", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(f => f.Kind == FeedbackKind.Owner);
        }
        else if (string.Equals(kind, "customer", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(f => f.Kind == FeedbackKind.Customer);
        }

        var rows = await query
            .OrderByDescending(f => f.CreatedAt)
            .Take(300)
            .Select(f => new { Feedback = f, f.Restaurant.Name, f.Restaurant.LogoUrl })
            .ToListAsync(ct);
        return rows.Select(r => ToDto(r.Feedback, r.Name, r.LogoUrl, null)).ToList();
    }

    public async Task<FeedbackDto> SetPublishedAsync(Guid id, bool isPublished, CancellationToken ct = default)
    {
        var feedback = await db.Feedback.Include(f => f.Restaurant).FirstOrDefaultAsync(f => f.Id == id, ct)
            ?? throw new NotFoundException("Feedback not found.");
        feedback.IsPublished = isPublished;
        await db.SaveChangesAsync(ct);
        return ToDto(feedback, feedback.Restaurant.Name, feedback.Restaurant.LogoUrl, null);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var deleted = await db.Feedback.Where(f => f.Id == id).ExecuteDeleteAsync(ct);
        if (deleted == 0)
        {
            throw new NotFoundException("Feedback not found.");
        }
    }

    // ---------- Landing page ----------

    public async Task<List<TestimonialDto>> GetTestimonialsAsync(CancellationToken ct = default)
    {
        var rows = await db.Feedback.AsNoTracking()
            .Where(f => f.IsPublished && f.Restaurant.IsActive)
            // Owners first (they speak about QRenvo itself), then the newest.
            .OrderBy(f => f.Kind == FeedbackKind.Owner ? 0 : 1)
            .ThenByDescending(f => f.UpdatedAt)
            .Take(LandingLimit)
            .Select(f => new { f.Kind, f.Name, f.Rating, f.Comment, f.ImageUrl, Restaurant = f.Restaurant.Name, f.Restaurant.LogoUrl })
            .ToListAsync(ct);

        return rows.Select(r => new TestimonialDto(
                r.Name,
                r.Kind == FeedbackKind.Owner ? $"Owner, {r.Restaurant}" : $"Guest at {r.Restaurant}",
                r.Rating,
                r.Comment,
                Absolute(r.ImageUrl ?? (r.Kind == FeedbackKind.Owner ? r.LogoUrl : null)),
                r.Kind.ToString().ToLowerInvariant()))
            .ToList();
    }

    // ---------- Helpers ----------

    private async Task<(Order Order, Restaurant Restaurant)> FindOrderAsync(string slug, Guid orderId, CancellationToken ct)
    {
        var row = await db.Orders.AsNoTracking()
            .Where(o => o.Id == orderId && o.Restaurant.Slug == slug)
            .Select(o => new { Order = o, o.Restaurant })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Order not found.");
        return (row.Order, row.Restaurant);
    }

    /// <summary>A small copy of our own photos, as a full address for the landing page (it lives on another host).</summary>
    private string? Absolute(string? url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return null;
        }
        var path = url.StartsWith("/uploads/", StringComparison.Ordinal) ? "/uploads/w160/" + url["/uploads/".Length..] : url;
        return path.StartsWith('/') ? siteOptions.Value.AppUrl.TrimEnd('/') + path : path;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static FeedbackDto ToDto(Feedback f, string restaurantName, string? restaurantLogo, string? tableNumber) => new(
        f.Id,
        f.Kind.ToString(),
        f.Rating,
        f.Name,
        f.Comment,
        f.ImageUrl,
        f.ImageUrl ?? (f.Kind == FeedbackKind.Owner ? restaurantLogo : null),
        restaurantName,
        tableNumber,
        f.IsPublished,
        f.CreatedAt);
}

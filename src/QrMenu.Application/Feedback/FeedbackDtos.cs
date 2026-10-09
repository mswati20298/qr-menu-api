using FluentValidation;

namespace QrMenu.Application.Feedbacks;

/// <summary>
/// One rating as shown in the panels. DisplayImageUrl: the uploaded photo, otherwise (owner feedback) the restaurant
/// logo, otherwise null (the page shows the name's first letter).
/// </summary>
public record FeedbackDto(
    Guid Id,
    string Kind,
    int Rating,
    string Name,
    string? Comment,
    string? ImageUrl,
    string? DisplayImageUrl,
    string RestaurantName,
    string? TableNumber,
    bool IsPublished,
    DateTime CreatedAt);

/// <summary>Guest rating after an order. Name is optional ("Guest").</summary>
public record SubmitCustomerFeedbackRequest(int Rating, string? Name, string? Comment, string? ImageUrl);

/// <summary>Restaurant owner rating QRenvo. ImageUrl optional: without it the restaurant logo is shown.</summary>
public record SubmitOwnerFeedbackRequest(int Rating, string Name, string? Comment, string? ImageUrl);

/// <summary>What the restaurant's guests said: average, count per star (index 0 = 1 star) and the latest ones.</summary>
public record CustomerFeedbackSummaryDto(double Average, int Count, List<int> CountByStars, List<FeedbackDto> Items);

/// <summary>A published rating for the landing page. Image URLs are absolute (the landing page is on another host).</summary>
public record TestimonialDto(string Name, string Role, int Rating, string? Comment, string? ImageUrl, string Kind);

public record SetFeedbackPublishedRequest(bool IsPublished);

internal static class FeedbackRules
{
    // Photos must come from our own upload endpoint, never an outside link.
    public static bool IsOwnUpload(string? url) =>
        string.IsNullOrEmpty(url)
        || (url.Length <= 200 && url.StartsWith("/uploads/", StringComparison.Ordinal)
            && url.Skip(9).All(c => char.IsLetterOrDigit(c) || c is '-' or '.'));
}

public class SubmitCustomerFeedbackRequestValidator : AbstractValidator<SubmitCustomerFeedbackRequest>
{
    public SubmitCustomerFeedbackRequestValidator()
    {
        RuleFor(x => x.Rating).InclusiveBetween(1, 5).WithMessage("Please choose 1 to 5 stars.");
        RuleFor(x => x.Name).MaximumLength(60);
        RuleFor(x => x.Comment).MaximumLength(600).WithMessage("Please keep it under 600 characters.");
        RuleFor(x => x.ImageUrl).Must(FeedbackRules.IsOwnUpload).WithMessage("Please upload the photo again.");
    }
}

public class SubmitOwnerFeedbackRequestValidator : AbstractValidator<SubmitOwnerFeedbackRequest>
{
    public SubmitOwnerFeedbackRequestValidator()
    {
        RuleFor(x => x.Rating).InclusiveBetween(1, 5).WithMessage("Please choose 1 to 5 stars.");
        RuleFor(x => x.Name).NotEmpty().WithMessage("Please enter your name.").MaximumLength(60);
        RuleFor(x => x.Comment).MaximumLength(600).WithMessage("Please keep it under 600 characters.");
        RuleFor(x => x.ImageUrl).Must(FeedbackRules.IsOwnUpload).WithMessage("Please upload the photo again.");
    }
}

public interface IFeedbackService
{
    // Guests (no login; the order id is the key, it is a random GUID only that phone knows).
    Task<FeedbackDto?> GetForOrderAsync(string slug, Guid orderId, CancellationToken ct = default);
    Task<FeedbackDto> SubmitForOrderAsync(string slug, Guid orderId, SubmitCustomerFeedbackRequest request, CancellationToken ct = default);

    /// <summary>Throws when the order does not exist for that restaurant (guards the guest photo upload).</summary>
    Task EnsureOrderExistsAsync(string slug, Guid orderId, CancellationToken ct = default);

    // Restaurant owner.
    Task<CustomerFeedbackSummaryDto> GetCustomerFeedbackAsync(Guid restaurantId, CancellationToken ct = default);
    Task<FeedbackDto?> GetOwnerFeedbackAsync(Guid restaurantId, CancellationToken ct = default);
    Task<FeedbackDto> SaveOwnerFeedbackAsync(Guid restaurantId, SubmitOwnerFeedbackRequest request, CancellationToken ct = default);

    // Super admin.
    /// <summary>kind: "owner", "customer" or empty for both.</summary>
    Task<List<FeedbackDto>> ListAllAsync(string? kind, CancellationToken ct = default);
    Task<FeedbackDto> SetPublishedAsync(Guid id, bool isPublished, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    // Landing page.
    Task<List<TestimonialDto>> GetTestimonialsAsync(CancellationToken ct = default);
}

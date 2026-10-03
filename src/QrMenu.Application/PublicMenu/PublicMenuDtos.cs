namespace QrMenu.Application.PublicMenu;

public record PublicMenuResponse(
    PublicRestaurantDto Restaurant,
    List<PublicCategoryDto> Categories);

public record PublicRestaurantDto(
    string Name,
    string Slug,
    string? Tagline,
    string? LogoUrl,
    string? CoverImageUrl,
    string WhatsAppNumber,
    string OpenTime,
    string CloseTime,
    bool IsOpenNow,
    bool IsGstEnabled,
    decimal GstPercentage,
    bool IsServiceChargeEnabled,
    decimal ServiceChargePercentage,
    bool ShowWelcomeMessage,
    string? WelcomeMessage,
    string BackgroundMode,
    List<PublicBackgroundDto> Backgrounds,
    string ThemeColor,
    // False when the restaurant's plan has expired (past grace) or was cancelled.
    bool OrderingEnabled,
    // Null when the restaurant has not set up UPI payments.
    string? UpiId,
    string? UpiPayeeName);

public record PublicBackgroundDto(string ImageUrl, int Slots, bool IsDefault);

public record PublicCategoryDto(Guid Id, string Name, int SortOrder, List<PublicMenuItemDto> Items);

public record PublicItemVariantDto(Guid Id, string Name, decimal Price, bool IsDefault);

public record PublicItemAddOnDto(Guid Id, string Name, decimal Price);

public record PublicMenuItemDto(
    Guid Id,
    string Name,
    string? Description,
    decimal Price,
    string? ImageUrl,
    bool IsVeg,
    string? Tag,
    bool IsAvailable,
    List<PublicItemVariantDto> Variants,
    List<PublicItemAddOnDto> AddOns);

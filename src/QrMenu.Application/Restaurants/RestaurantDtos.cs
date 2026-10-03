namespace QrMenu.Application.Restaurants;

public record RestaurantDto(
    Guid Id,
    string Name,
    string Slug,
    string? Tagline,
    string? Address,
    string? Phone,
    string WhatsAppNumber,
    string OpenTime,
    string CloseTime,
    string? LogoUrl,
    string? CoverImageUrl,
    bool IsActive,
    bool IsGstEnabled,
    decimal GstPercentage,
    bool IsServiceChargeEnabled,
    decimal ServiceChargePercentage,
    bool ShowWelcomeMessage,
    string? WelcomeMessage,
    string ThemeColor,
    string? GstNumber,
    string InvoicePrefix,
    string? UpiId,
    string? UpiPayeeName,
    bool KitchenLoginEnabled);

public record UpdateRestaurantRequest(
    string Name,
    string? Tagline,
    string? Address,
    string? Phone,
    string WhatsAppNumber,
    string OpenTime,
    string CloseTime,
    string? LogoUrl,
    string? CoverImageUrl,
    bool IsGstEnabled,
    decimal GstPercentage,
    bool IsServiceChargeEnabled,
    decimal ServiceChargePercentage,
    bool ShowWelcomeMessage,
    string? WelcomeMessage,
    string? ThemeColor = null,
    string? GstNumber = null,
    string? InvoicePrefix = null,
    string? UpiId = null,
    string? UpiPayeeName = null);

/// <summary>Pin = 4 to 8 digits, or null/empty to turn the separate kitchen login off.</summary>
public record SetKitchenPinRequest(string? Pin);

public record ScanStatsDto(DateOnly Date, int Count);

public record DailyRevenueDto(DateOnly Date, decimal Revenue, int OrderCount);

public record TopSellingItemDto(string Name, int QtySold);

public record RecentOrderDto(
    Guid Id,
    string? TableNumber,
    string ItemsSummary,
    decimal Total,
    string Status,
    DateTime CreatedAt);

public record DashboardStatsDto(
    int TotalOrdersToday,
    decimal TotalRevenueToday,
    decimal AverageOrderValueToday,
    int TotalOrdersYesterday,
    decimal TotalRevenueYesterday,
    decimal AverageOrderValueYesterday,
    int ActiveTables,
    int TotalTables,
    List<ScanStatsDto> ScansLast7Days,
    List<DailyRevenueDto> RevenueLast7Days,
    List<DailyRevenueDto> RevenueLast30Days,
    List<TopSellingItemDto> TopSellingItems,
    List<RecentOrderDto> RecentOrders);

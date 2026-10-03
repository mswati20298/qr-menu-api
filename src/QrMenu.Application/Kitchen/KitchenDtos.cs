namespace QrMenu.Application.Kitchen;

public record KitchenLoginRequest(string Slug, string Pin);

public record KitchenAuthResponse(string Token, string RestaurantName, string RestaurantSlug);

public record KitchenOrderItemDto(string Name, string? Variant, List<string> AddOns, int Qty);

/// <summary>What the kitchen needs to cook: no prices, no phone numbers.</summary>
public record KitchenOrderDto(
    Guid Id,
    string? TableNumber,
    string? CustomerName,
    string? Note,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    List<KitchenOrderItemDto> Items);

/// <summary>New and Preparing orders, plus orders served in the last 2 hours.</summary>
public record KitchenBoardDto(string RestaurantName, DateTime ServerTime, List<KitchenOrderDto> Orders);

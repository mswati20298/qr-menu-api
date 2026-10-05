namespace QrMenu.Application.Auth;

public record RegisterRequest(
    string RestaurantName,
    string OwnerName,
    string Email,
    string Password,
    string WhatsAppNumber);

public record LoginRequest(string Email, string Password);

public record AuthResponse(string Token, string OwnerName, Guid RestaurantId, string RestaurantSlug, string RestaurantName);

/// <summary>Owner changes their own password.</summary>
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

using QrMenu.Domain.Entities;

namespace QrMenu.Application.Common.Interfaces;

public interface IJwtTokenService
{
    string GenerateToken(User user);

    /// <summary>Token for a platform administrator: carries a superAdmin claim and NO restaurantId.</summary>
    string GenerateSuperAdminToken(SuperAdmin admin);

    /// <summary>Token for a kitchen screen: restaurantId + kitchen claim, accepted only by the kitchen endpoints.</summary>
    string GenerateKitchenToken(Restaurant restaurant);
}

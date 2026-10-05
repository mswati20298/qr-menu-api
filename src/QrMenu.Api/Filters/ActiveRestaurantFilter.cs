using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using QrMenu.Application.Common.Exceptions;
using QrMenu.Infrastructure.Persistence;

namespace QrMenu.Api.Filters;

/// <summary>
/// Runs for every owner and kitchen endpoint: a restaurant that was suspended by the super admin is cut off
/// immediately, even if its login token has not expired yet. A kitchen screen is also signed out as soon as
/// the owner changes or removes the kitchen PIN.
/// </summary>
public class ActiveRestaurantFilter(AppDbContext db) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var user = context.HttpContext.User;
        var claim = user.FindFirst("restaurantId")?.Value;

        if (Guid.TryParse(claim, out var restaurantId))
        {
            var restaurant = await db.Restaurants
                .AsNoTracking()
                .Where(r => r.Id == restaurantId)
                .Select(r => new { r.IsActive, HasKitchenPin = r.KitchenPinHash != null, r.KitchenPinVersion })
                .FirstOrDefaultAsync(context.HttpContext.RequestAborted);

            if (restaurant is null || !restaurant.IsActive)
            {
                throw new ForbiddenException(
                    "This restaurant account is suspended. Please contact support.",
                    "restaurant_suspended");
            }

            if (user.HasClaim("kitchen", "true")
                && (!restaurant.HasKitchenPin || user.FindFirst("kitchenVersion")?.Value != restaurant.KitchenPinVersion.ToString()))
            {
                throw new UnauthorizedAppException("The kitchen PIN was changed. Please sign in again.");
            }

            // Owner tokens: refused once the password has been changed or reset since the token was issued.
            if (!user.HasClaim("kitchen", "true")
                && Guid.TryParse(user.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId))
            {
                var passwordVersion = await db.Users
                    .AsNoTracking()
                    .Where(u => u.Id == userId && u.RestaurantId == restaurantId)
                    .Select(u => (int?)u.PasswordVersion)
                    .FirstOrDefaultAsync(context.HttpContext.RequestAborted);

                if (passwordVersion is null || (user.FindFirst("pwdv")?.Value ?? "0") != passwordVersion.Value.ToString())
                {
                    throw new UnauthorizedAppException("Your password was changed. Please sign in again.");
                }
            }
        }

        await next();
    }
}

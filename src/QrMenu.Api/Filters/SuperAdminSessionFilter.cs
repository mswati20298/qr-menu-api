using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.Filters;
using QrMenu.Application.Common.Exceptions;
using QrMenu.Application.SuperAdmins;

namespace QrMenu.Api.Filters;

/// <summary>
/// Super admin tokens stop working once that admin changes the password (or is removed), instead of living on
/// until they expire. One small query per request; the super admin panel is used by very few people.
/// </summary>
public class SuperAdminSessionFilter(ISuperAdminService superAdmins) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var user = context.HttpContext.User;
        if (Guid.TryParse(user.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var adminId))
        {
            var version = await superAdmins.GetPasswordVersionAsync(adminId, context.HttpContext.RequestAborted);
            if (version is null || (user.FindFirst("pwdv")?.Value ?? "0") != version.Value.ToString())
            {
                throw new UnauthorizedAppException("Your password was changed. Please sign in again.");
            }
        }
        await next();
    }
}

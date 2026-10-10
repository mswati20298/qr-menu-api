using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using QrMenu.Application.SuperAdmins;

namespace QrMenu.Api.Filters;

/// <summary>
/// Records every change a super admin makes (anything but GET), including refused and failed attempts.
/// Only the action, address, id and result are kept; never the request body.
/// </summary>
public class AdminAuditFilter(IAdminAuditService audit) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var http = context.HttpContext;
        if (HttpMethods.IsGet(http.Request.Method) || HttpMethods.IsHead(http.Request.Method))
        {
            await next();
            return;
        }

        var executed = await next();

        // Errors are turned into responses by the middleware after this filter, so work out the code here.
        var status = executed.Exception is null || executed.ExceptionHandled
            ? (executed.Result as Microsoft.AspNetCore.Mvc.Infrastructure.IStatusCodeActionResult)?.StatusCode ?? http.Response.StatusCode
            : QrMenu.Api.Middleware.ExceptionHandlingMiddleware.StatusCodeFor(executed.Exception);

        var action = (context.ActionDescriptor as ControllerActionDescriptor)?.ActionName ?? context.ActionDescriptor.DisplayName ?? "?";
        var route = (context.ActionDescriptor as ControllerActionDescriptor)?.AttributeRouteInfo?.Template ?? http.Request.Path.ToString();
        var target = context.RouteData.Values.TryGetValue("id", out var id) ? id?.ToString() : null;

        await audit.WriteAsync(new AdminAuditEntry(
            http.User.FindFirst(ClaimTypes.Email)?.Value ?? "unknown",
            action,
            $"{http.Request.Method} /{route}",
            target,
            status,
            http.Connection.RemoteIpAddress?.ToString()));
    }

}

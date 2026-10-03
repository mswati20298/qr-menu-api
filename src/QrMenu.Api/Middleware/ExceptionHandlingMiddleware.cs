using System.Net;
using System.Text.Json;
using FluentValidation;
using QrMenu.Application.Common.Exceptions;

namespace QrMenu.Api.Middleware;

public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleAsync(context, ex);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception exception)
    {
        var (statusCode, message, errors) = exception switch
        {
            ValidationException validationEx => (
                HttpStatusCode.BadRequest,
                "One or more validation errors occurred.",
                validationEx.Errors.Select(e => e.ErrorMessage).ToArray()),
            NotFoundException notFoundEx => (HttpStatusCode.NotFound, notFoundEx.Message, Array.Empty<string>()),
            ConflictException conflictEx => (HttpStatusCode.Conflict, conflictEx.Message, Array.Empty<string>()),
            UnauthorizedAppException unauthorizedEx => (HttpStatusCode.Unauthorized, unauthorizedEx.Message, Array.Empty<string>()),
            ForbiddenException forbiddenEx => (HttpStatusCode.Forbidden, forbiddenEx.Message, Array.Empty<string>()),
            TooManyAttemptsException tooManyEx => (HttpStatusCode.TooManyRequests, tooManyEx.Message, Array.Empty<string>()),
            _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred. Please try again later.", Array.Empty<string>())
        };

        if (statusCode == HttpStatusCode.InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception");
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var code = (exception as ForbiddenException)?.Code;
        var payload = JsonSerializer.Serialize(new { message, errors, code });
        await context.Response.WriteAsync(payload);
    }
}

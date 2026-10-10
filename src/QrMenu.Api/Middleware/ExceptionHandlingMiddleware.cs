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

    /// <summary>SQL Server 2601 / 2627: a unique index or key refused a duplicate row.</summary>
    private static bool IsDuplicateKey(Microsoft.EntityFrameworkCore.DbUpdateException ex) =>
        ex.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 };

    /// <summary>The HTTP status an exception becomes (also used by the audit log, which runs before this middleware answers).</summary>
    public static int StatusCodeFor(Exception exception) => exception switch
    {
        ValidationException => StatusCodes.Status400BadRequest,
        NotFoundException => StatusCodes.Status404NotFound,
        ConflictException => StatusCodes.Status409Conflict,
        UnauthorizedAppException => StatusCodes.Status401Unauthorized,
        ForbiddenException => StatusCodes.Status403Forbidden,
        TooManyAttemptsException => StatusCodes.Status429TooManyRequests,
        _ => StatusCodes.Status500InternalServerError
    };

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
            Microsoft.EntityFrameworkCore.DbUpdateException dbEx when IsDuplicateKey(dbEx) =>
                (HttpStatusCode.Conflict, "This already exists. Please use a different name.", Array.Empty<string>()),
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

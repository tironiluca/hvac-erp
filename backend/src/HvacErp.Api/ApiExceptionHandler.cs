using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using HvacErp.Application.Common;

namespace HvacErp.Api;

/// <summary>Single place that maps domain exceptions to RFC 7807 problem responses: <see cref="Application.Common.NotFoundException"/> → 404, <see cref="Application.Common.BusinessRuleException"/> → 400, <see cref="Application.Common.ForbiddenException"/> → 403, <see cref="Application.Common.ConflictException"/> → 409, Postgres unique / check / FK violations → 409 / 400, anything else → 500.</summary>
public class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title, detail) = exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Not found", exception.Message),
            BusinessRuleException => (StatusCodes.Status400BadRequest, "Business rule violation", exception.Message),
            ConflictException => (StatusCodes.Status409Conflict, "Conflict", exception.Message),
            ForbiddenException => (StatusCodes.Status403Forbidden, "Forbidden", exception.Message),
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } } =>
                (StatusCodes.Status409Conflict, "Duplicate value", "A record with this value already exists."),
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.CheckViolation } } =>
                (StatusCodes.Status400BadRequest, "Invalid value", "One or more values violate a database integrity rule."),
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation } } =>
                (StatusCodes.Status400BadRequest, "Invalid reference", "A referenced record (lookup code or related row) does not exist."),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected error", exception.Message)
        };

        if (status == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception");
            // The handler swallows the exception, so report it explicitly (no-op without a Sentry DSN).
            Sentry.SentrySdk.CaptureException(exception);
        }
        else if (status == StatusCodes.Status409Conflict)
        {
            logger.LogWarning(exception, "Unique constraint violation");
        }

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status,
            Title = title,
            // Never echo internal exception text to the caller on a 500; the correlation id finds it in the logs.
            Detail = status == StatusCodes.Status500InternalServerError ? "An unexpected error occurred." : detail,
            Extensions = { ["correlationId"] = httpContext.TraceIdentifier },
        }, cancellationToken);

        return true;
    }
}

using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HvacErp.Api.Observability;

/// <summary>
/// Anonymous health endpoints. <c>/health/live</c> runs no checks (process is up — use it for the
/// Render health-check path so a Supabase blip doesn't restart the container); <c>/health</c> runs the
/// readiness checks (database + Storage) and returns 503 when any fails. Bodies list only check names,
/// statuses and durations — never exception details.
/// </summary>
public static class HealthEndpoints
{
    /// <summary>Maps <c>/health/live</c> and <c>/health</c>.</summary>
    public static IEndpointRouteBuilder MapAppHealthChecks(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = WriteJsonAsync,
        }).AllowAnonymous();

        endpoints.MapHealthChecks("/health", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(Infrastructure.DependencyInjection.ReadinessTag),
            ResponseWriter = WriteJsonAsync,
        }).AllowAnonymous();

        return endpoints;
    }

    private static Task WriteJsonAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        context.Response.Headers.CacheControl = "no-store";
        return context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            durationMs = Math.Round(report.TotalDuration.TotalMilliseconds, 1),
            checks = report.Entries.ToDictionary(
                e => e.Key,
                e => new { status = e.Value.Status.ToString(), durationMs = Math.Round(e.Value.Duration.TotalMilliseconds, 1) }),
        }, JsonOptions));
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}

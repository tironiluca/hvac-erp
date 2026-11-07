using System.Diagnostics;
using System.Text.RegularExpressions;

namespace HvacErp.Api.Observability;

/// <summary>
/// Gives every request a correlation id and one structured completion log line.
/// <list type="bullet">
/// <item>Accepts a well-formed inbound <c>X-Correlation-Id</c> (the SPA sends one per call) or mints one.</item>
/// <item>Echoes it on the response, stores it as <see cref="HttpContext.TraceIdentifier"/> (so problem
/// responses carry it) and opens a logging scope, so every log line of the request is searchable by it.</item>
/// <item>Logs <c>HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs} ms</c> at completion —
/// Warning for 5xx, Information otherwise. Liveness probes are not logged.</item>
/// </list>
/// </summary>
public sealed partial class RequestCorrelationMiddleware(RequestDelegate next, ILogger<RequestCorrelationMiddleware> logger)
{
    /// <summary>Request / response header carrying the id.</summary>
    public const string HeaderName = "X-Correlation-Id";

    /// <summary>Handles one request.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[HeaderName].ToString();
        if (!SafeId().IsMatch(correlationId))
        {
            correlationId = Guid.NewGuid().ToString("N");
        }

        context.TraceIdentifier = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });
        Sentry.SentrySdk.ConfigureScope(scope => scope.SetTag("correlation_id", correlationId));

        using var scope = logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId });
        var started = Stopwatch.GetTimestamp();
        try
        {
            await next(context);
        }
        finally
        {
            if (!context.Request.Path.StartsWithSegments("/health/live"))
            {
                var elapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                var status = context.Response.StatusCode;
                logger.Log(status >= 500 ? LogLevel.Warning : LogLevel.Information,
                    "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs:0.0} ms",
                    context.Request.Method, context.Request.Path.Value, status, elapsedMs);
            }
        }
    }

    [GeneratedRegex("^[A-Za-z0-9._-]{8,64}$")]
    private static partial Regex SafeId();
}

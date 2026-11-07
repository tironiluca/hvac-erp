using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HvacErp.Infrastructure.Health;

/// <summary>
/// Readiness probe for Supabase Storage: fetches the documents bucket's metadata with the
/// service-role client. Healthy only when the bucket answers 200 — which proves the host is
/// reachable, the key is valid and the bucket exists (what PDF generation and uploads need).
/// </summary>
public sealed class SupabaseStorageHealthCheck(HttpClient http, IConfiguration configuration) : IHealthCheck
{
    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var bucket = configuration["Supabase:StorageBucket"];
        if (string.IsNullOrWhiteSpace(bucket))
        {
            return HealthCheckResult.Unhealthy("Supabase:StorageBucket is not configured.");
        }

        try
        {
            using var response = await http.GetAsync($"storage/v1/bucket/{bucket}", cancellationToken);
            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy($"Bucket lookup returned HTTP {(int)response.StatusCode}.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return HealthCheckResult.Unhealthy("Supabase Storage is unreachable.", ex);
        }
    }
}

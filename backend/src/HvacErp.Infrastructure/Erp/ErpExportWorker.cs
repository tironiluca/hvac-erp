using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using HvacErp.Application.Erp;

using HvacErp.Infrastructure.Persistence;

namespace HvacErp.Infrastructure.Erp;

/// <summary>
/// Sends due ERP exports every <c>Erp:PollSeconds</c>. Idle (one cheap check) while the integration
/// is off; failures are recorded on the export rows, never crash the API.
/// </summary>
public sealed class ErpExportWorker(
    IServiceScopeFactory scopes,
    IOptionsMonitor<ErpOptions> options,
    ILogger<ErpExportWorker> logger) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                // The outbox holds every tenant's orders and no caller stands behind the worker.
                using var system = scope.ServiceProvider.GetRequiredService<AppDbContext>().AsSystem();
                var exports = scope.ServiceProvider.GetRequiredService<IErpExportService>();
                if (exports.Enabled)
                {
                    var sent = await exports.ProcessDueAsync(cancellationToken: stoppingToken);
                    if (sent > 0)
                    {
                        logger.LogInformation("Processed {Count} ERP order export(s)", sent);
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "ERP export run failed; retrying on the next tick");
            }

            await Task.Delay(TimeSpan.FromSeconds(Math.Max(5, options.CurrentValue.PollSeconds)), stoppingToken);
        }
    }
}

using Microsoft.AspNetCore.SignalR;

namespace HvacErp.Api.Hubs.Presence;

/// <summary>
/// Drives <see cref="ICalendarPresenceStore.HeartbeatAsync"/> every <see cref="Interval"/> and, when
/// stale viewers were pruned, re-broadcasts the roster so open calendars drop the ghosts.
/// </summary>
public sealed class PresenceHeartbeatService(
    ICalendarPresenceStore store,
    IHubContext<InstallationCalendarHub, IInstallationCalendarClient> hub,
    ILogger<PresenceHeartbeatService> logger) : BackgroundService
{
    /// <summary>Heartbeat period.</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                if (await store.HeartbeatAsync())
                {
                    await hub.Clients.All.PresenceChanged(await store.GetRosterAsync());
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Presence is best-effort; never let a Redis blip take the API down.
                logger.LogWarning(ex, "Calendar presence heartbeat failed");
            }
        }
    }
}

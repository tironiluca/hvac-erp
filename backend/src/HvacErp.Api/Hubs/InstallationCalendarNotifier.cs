using Microsoft.AspNetCore.SignalR;
using HvacErp.Application.Common;
using HvacErp.Application.Dtos;

namespace HvacErp.Api.Hubs;

/// <summary><see cref="IInstallationCalendarNotifier"/> over the SignalR <see cref="InstallationCalendarHub"/>.</summary>
public sealed class InstallationCalendarNotifier(IHubContext<InstallationCalendarHub, IInstallationCalendarClient> hub)
    : IInstallationCalendarNotifier
{
    /// <inheritdoc />
    public Task InstallationChangedAsync(ScheduledInstallationDto installation, CancellationToken cancellationToken = default) =>
        hub.Clients.All.InstallationChanged(installation);

    /// <inheritdoc />
    public Task InstallationDeletedAsync(int installationId, CancellationToken cancellationToken = default) =>
        hub.Clients.All.InstallationDeleted(installationId);
}

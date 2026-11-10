using Microsoft.AspNetCore.SignalR;
using HvacErp.Application.Common;
using HvacErp.Application.Dtos;

namespace HvacErp.Api.Hubs;

/// <summary><see cref="IInstallationCalendarNotifier"/> over the SignalR <see cref="InstallationCalendarHub"/>, one group per store.</summary>
public sealed class InstallationCalendarNotifier(IHubContext<InstallationCalendarHub, IInstallationCalendarClient> hub)
    : IInstallationCalendarNotifier
{
    /// <inheritdoc />
    public Task InstallationChangedAsync(ScheduledInstallationDto installation, CancellationToken cancellationToken = default) =>
        hub.Clients.Group(CalendarGroups.Store(installation.StoreId)).InstallationChanged(installation);

    /// <inheritdoc />
    public Task InstallationDeletedAsync(int installationId, int storeId, CancellationToken cancellationToken = default) =>
        hub.Clients.Group(CalendarGroups.Store(storeId)).InstallationDeleted(installationId);
}

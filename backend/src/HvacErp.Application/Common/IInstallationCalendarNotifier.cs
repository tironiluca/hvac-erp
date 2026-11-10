using HvacErp.Application.Dtos;

namespace HvacErp.Application.Common;

/// <summary>
/// Fans out <c>ScheduledInstallation</c> changes to the clients watching the shared real-time
/// calendar of the installation's store — never to another store's or tenant's viewers. Implemented in the API layer over a SignalR hub; a no-op double is fine
/// for seeding / tests. Application services call this after a successful save so one person
/// moving an appointment updates everyone else's view without a refresh.
/// </summary>
public interface IInstallationCalendarNotifier
{
    /// <summary>Broadcasts a created / edited / moved installation.</summary>
    Task InstallationChangedAsync(ScheduledInstallationDto installation, CancellationToken cancellationToken = default);
    /// <summary>Broadcasts a cancelled installation to the viewers of <paramref name="storeId"/>.</summary>
    Task InstallationDeletedAsync(int installationId, int storeId, CancellationToken cancellationToken = default);
}

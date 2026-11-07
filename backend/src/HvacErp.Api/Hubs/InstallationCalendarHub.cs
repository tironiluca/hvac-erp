using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using HvacErp.Api.Hubs.Presence;
using HvacErp.Application.Dtos;

namespace HvacErp.Api.Hubs;

/// <summary>
/// Server-push channel for the shared installation calendar. Every authenticated client connects
/// once; the server broadcasts <c>ScheduledInstallation</c> changes as they are saved so
/// drag-and-drop reschedules land on everyone's grid immediately, and it keeps a live roster of
/// who currently has the calendar open (presence) so each viewer can see the others. Clients
/// never invoke hub methods — this is one-directional fan-out.
///
/// Presence lives in an <see cref="ICalendarPresenceStore"/>: in-process by default (exact for one
/// instance), or Redis together with the SignalR Redis backplane when <c>Redis:ConnectionString</c>
/// is set, so both the change fan-out and the roster span every instance. The frontend treats an
/// empty roster as "unknown", never as "nobody".
/// </summary>
[Authorize]
public sealed class InstallationCalendarHub(ICalendarPresenceStore presence) : Hub<IInstallationCalendarClient>
{
    /// <inheritdoc />
    public override async Task OnConnectedAsync()
    {
        await presence.AddAsync(Context.ConnectionId, new CalendarPresenceDto(
            Context.User?.FindFirstValue("app_user_id")
                ?? Context.User?.FindFirstValue("sub")
                ?? Context.ConnectionId,
            Context.User?.FindFirstValue("email")
                ?? Context.User?.FindFirstValue("sub")
                ?? "Utente"));

        await base.OnConnectedAsync();
        await BroadcastPresenceAsync();
    }

    /// <inheritdoc />
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await presence.RemoveAsync(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
        await BroadcastPresenceAsync();
    }

    private async Task BroadcastPresenceAsync() =>
        await Clients.All.PresenceChanged(await presence.GetRosterAsync());
}

/// <summary>Strongly-typed client contract mirrored by <c>installation-realtime.service.ts</c> on the frontend.</summary>
public interface IInstallationCalendarClient
{
    /// <summary>An installation was created, edited or moved.</summary>
    Task InstallationChanged(ScheduledInstallationDto installation);
    /// <summary>An installation was cancelled.</summary>
    Task InstallationDeleted(int installationId);

    /// <summary>The full set of users currently viewing the calendar (includes the recipient).</summary>
    Task PresenceChanged(IReadOnlyList<CalendarPresenceDto> viewers);
}

using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using HvacErp.Api.Hubs.Presence;
using HvacErp.Application.Common;
using HvacErp.Application.Dtos;

namespace HvacErp.Api.Hubs;

/// <summary>
/// Server-push channel for the shared installation calendar. Every member connects once; the server
/// broadcasts <c>ScheduledInstallation</c> changes as they are saved so drag-and-drop reschedules
/// land on everyone's grid immediately, and it keeps a live roster of who currently has the
/// calendar open (presence) so each viewer can see the others. Clients never invoke hub methods —
/// this is one-directional fan-out.
/// <para>
/// Fan-out is scoped: a connection joins one group per store its request may read (the tenant and
/// store come from the same claims as a REST call, selected with <c>?tenant=</c> / <c>?store=</c>)
/// and one group for its tenant's presence roster. Nothing is ever sent to all connections.
/// </para>
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
        // [Authorize] is the member policy, so the tenant claim is always there.
        var tenantId = TenantId ?? throw new HubException("No tenant.");
        await Groups.AddToGroupAsync(Context.ConnectionId, CalendarGroups.Tenant(tenantId));
        foreach (var storeId in Context.User!.FindAll(AppClaimTypes.StoreId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, CalendarGroups.Store(storeId.Value));
        }

        await presence.AddAsync(Context.ConnectionId, tenantId, new CalendarPresenceDto(
            Context.User?.FindFirstValue(AppClaimTypes.UserId)
                ?? Context.User?.FindFirstValue("sub")
                ?? Context.ConnectionId,
            Context.User?.FindFirstValue("email")
                ?? Context.User?.FindFirstValue("sub")
                ?? "User"));
        await base.OnConnectedAsync();
        await BroadcastPresenceAsync(tenantId);
    }

    /// <inheritdoc />
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await presence.RemoveAsync(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
        if (TenantId is { } tenantId)
        {
            await BroadcastPresenceAsync(tenantId);
        }
    }

    private int? TenantId =>
        int.TryParse(Context.User?.FindFirstValue(AppClaimTypes.TenantId), NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : null;

    private async Task BroadcastPresenceAsync(int tenantId) =>
        await Clients.Group(CalendarGroups.Tenant(tenantId)).PresenceChanged(await presence.GetRosterAsync(tenantId));
}

/// <summary>SignalR group names of the calendar hub.</summary>
public static class CalendarGroups
{
    /// <summary>Everyone of a tenant with the calendar open (presence roster).</summary>
    public static string Tenant(int tenantId) => $"tenant:{tenantId.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>Everyone watching a store's installations.</summary>
    public static string Store(int storeId) => Store(storeId.ToString(CultureInfo.InvariantCulture));

    /// <summary>Everyone watching a store's installations, from the store-id claim value.</summary>
    public static string Store(string storeId) => $"store:{storeId}";
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

using HvacErp.Application.Dtos;

namespace HvacErp.Api.Hubs.Presence;

/// <summary>
/// Who currently has the shared installation calendar open, keyed by SignalR connection id and
/// kept per tenant: a roster only ever lists colleagues. It collapses a person's multiple tabs into
/// one entry and is ordered by name.
/// <para>
/// <see cref="InMemoryCalendarPresenceStore"/> is exact for a single API instance;
/// <see cref="RedisCalendarPresenceStore"/> shares the roster across instances (enabled together
/// with the SignalR Redis backplane when <c>Redis:ConnectionString</c> is configured).
/// </para>
/// </summary>
public interface ICalendarPresenceStore
{
    /// <summary>Records a connection as present in <paramref name="tenantId"/>.</summary>
    Task AddAsync(string connectionId, int tenantId, CalendarPresenceDto member);

    /// <summary>Forgets a connection.</summary>
    Task RemoveAsync(string connectionId);

    /// <summary>The de-duplicated roster of <paramref name="tenantId"/>'s live viewers.</summary>
    Task<IReadOnlyList<CalendarPresenceDto>> GetRosterAsync(int tenantId);

    /// <summary>
    /// Periodic upkeep run by <see cref="PresenceHeartbeatService"/>: refreshes this instance's
    /// connections and drops entries whose instance stopped refreshing (crashed / scaled in).
    /// </summary>
    /// <returns>The tenants whose roster changed because stale entries were dropped.</returns>
    Task<IReadOnlyCollection<int>> HeartbeatAsync();
}

internal static class PresenceRoster
{
    public static IReadOnlyList<CalendarPresenceDto> Collapse(IEnumerable<CalendarPresenceDto> members) =>
        members
            .GroupBy(m => m.Id)
            .Select(g => g.First())
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
}

using HvacErp.Application.Dtos;

namespace HvacErp.Api.Hubs.Presence;

/// <summary>
/// Who currently has the shared installation calendar open, keyed by SignalR connection id. The
/// roster collapses a person's multiple tabs into one entry and is ordered by name.
/// <para>
/// <see cref="InMemoryCalendarPresenceStore"/> is exact for a single API instance;
/// <see cref="RedisCalendarPresenceStore"/> shares the roster across instances (enabled together
/// with the SignalR Redis backplane when <c>Redis:ConnectionString</c> is configured).
/// </para>
/// </summary>
public interface ICalendarPresenceStore
{
    /// <summary>Records a connection as present.</summary>
    Task AddAsync(string connectionId, CalendarPresenceDto member);

    /// <summary>Forgets a connection.</summary>
    Task RemoveAsync(string connectionId);

    /// <summary>The de-duplicated roster of live viewers.</summary>
    Task<IReadOnlyList<CalendarPresenceDto>> GetRosterAsync();

    /// <summary>
    /// Periodic upkeep run by <see cref="PresenceHeartbeatService"/>: refreshes this instance's
    /// connections and drops entries whose instance stopped refreshing (crashed / scaled in).
    /// </summary>
    /// <returns>True when stale entries were dropped, i.e. the roster changed.</returns>
    Task<bool> HeartbeatAsync();
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

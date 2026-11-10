using System.Collections.Concurrent;
using HvacErp.Application.Dtos;

namespace HvacErp.Api.Hubs.Presence;

/// <summary>Process-local presence (default). Exact only while the API runs a single instance.</summary>
public sealed class InMemoryCalendarPresenceStore : ICalendarPresenceStore
{
    private readonly ConcurrentDictionary<string, (int TenantId, CalendarPresenceDto Member)> _members = new();

    /// <inheritdoc />
    public Task AddAsync(string connectionId, int tenantId, CalendarPresenceDto member)
    {
        _members[connectionId] = (tenantId, member);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(string connectionId)
    {
        _members.TryRemove(connectionId, out _);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<CalendarPresenceDto>> GetRosterAsync(int tenantId) =>
        Task.FromResult(PresenceRoster.Collapse(_members.Values.Where(m => m.TenantId == tenantId).Select(m => m.Member)));

    /// <inheritdoc />
    public Task<IReadOnlyCollection<int>> HeartbeatAsync() => Task.FromResult<IReadOnlyCollection<int>>([]);
}

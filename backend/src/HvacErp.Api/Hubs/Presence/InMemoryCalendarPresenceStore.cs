using System.Collections.Concurrent;
using HvacErp.Application.Dtos;

namespace HvacErp.Api.Hubs.Presence;

/// <summary>Process-local presence (default). Exact only while the API runs a single instance.</summary>
public sealed class InMemoryCalendarPresenceStore : ICalendarPresenceStore
{
    private readonly ConcurrentDictionary<string, CalendarPresenceDto> _members = new();

    /// <inheritdoc />
    public Task AddAsync(string connectionId, CalendarPresenceDto member)
    {
        _members[connectionId] = member;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(string connectionId)
    {
        _members.TryRemove(connectionId, out _);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<CalendarPresenceDto>> GetRosterAsync() =>
        Task.FromResult(PresenceRoster.Collapse(_members.Values));

    /// <inheritdoc />
    public Task<bool> HeartbeatAsync() => Task.FromResult(false);
}

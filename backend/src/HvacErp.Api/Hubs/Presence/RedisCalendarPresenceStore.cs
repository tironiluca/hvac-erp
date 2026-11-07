using System.Collections.Concurrent;
using System.Text.Json;
using HvacErp.Application.Dtos;
using StackExchange.Redis;

namespace HvacErp.Api.Hubs.Presence;

/// <summary>
/// Presence shared by every API instance through Redis: a hash of connection id → member, plus a
/// sorted set of connection id → last-seen time. Each instance re-stamps its own connections on
/// every heartbeat; entries not refreshed within <see cref="Ttl"/> (their instance died without
/// running OnDisconnected) are pruned, so a crashed node can never leave ghosts on the roster.
/// </summary>
public sealed class RedisCalendarPresenceStore(IConnectionMultiplexer redis, TimeProvider clock, string keyPrefix = "hvac-erp:calendar-presence")
    : ICalendarPresenceStore
{
    /// <summary>How long an entry survives without a heartbeat (3 missed beats).</summary>
    public static readonly TimeSpan Ttl = PresenceHeartbeatService.Interval * 3;

    private readonly RedisKey _membersKey = $"{keyPrefix}:members";
    private readonly RedisKey _seenKey = $"{keyPrefix}:seen";
    private readonly ConcurrentDictionary<string, byte> _local = new();

    private IDatabase Db => redis.GetDatabase();
    private double Now => clock.GetUtcNow().ToUnixTimeMilliseconds();

    /// <inheritdoc />
    public async Task AddAsync(string connectionId, CalendarPresenceDto member)
    {
        _local[connectionId] = 0;
        var batch = Db.CreateBatch();
        var set = batch.HashSetAsync(_membersKey, connectionId, JsonSerializer.Serialize(member));
        var seen = batch.SortedSetAddAsync(_seenKey, connectionId, Now);
        batch.Execute();
        await Task.WhenAll(set, seen);
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string connectionId)
    {
        _local.TryRemove(connectionId, out _);
        var batch = Db.CreateBatch();
        var del = batch.HashDeleteAsync(_membersKey, connectionId);
        var rem = batch.SortedSetRemoveAsync(_seenKey, connectionId);
        batch.Execute();
        await Task.WhenAll(del, rem);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CalendarPresenceDto>> GetRosterAsync()
    {
        var live = await Db.SortedSetRangeByScoreAsync(_seenKey, Now - Ttl.TotalMilliseconds, double.PositiveInfinity);
        if (live.Length == 0)
        {
            return [];
        }

        var values = await Db.HashGetAsync(_membersKey, live);
        var members = values
            .Where(v => v.HasValue)
            .Select(v => JsonSerializer.Deserialize<CalendarPresenceDto>(v.ToString()))
            .OfType<CalendarPresenceDto>();
        return PresenceRoster.Collapse(members);
    }

    /// <inheritdoc />
    public async Task<bool> HeartbeatAsync()
    {
        var db = Db;
        var now = Now;
        if (!_local.IsEmpty)
        {
            await db.SortedSetAddAsync(_seenKey, _local.Keys.Select(id => new SortedSetEntry(id, now)).ToArray());
        }

        var stale = await db.SortedSetRangeByScoreAsync(_seenKey, double.NegativeInfinity, now - Ttl.TotalMilliseconds);
        if (stale.Length == 0)
        {
            return false;
        }

        await db.HashDeleteAsync(_membersKey, stale);
        await db.SortedSetRemoveAsync(_seenKey, stale);
        return true;
    }
}

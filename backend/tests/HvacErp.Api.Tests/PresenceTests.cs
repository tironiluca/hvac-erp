using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Http.Connections;
using HvacErp.Api.Hubs.Presence;
using HvacErp.Application.Dtos;
using StackExchange.Redis;

namespace HvacErp.Api.Tests;

public class InMemoryPresenceStoreTests
{
    [Fact]
    public async Task Roster_collapses_tabs_and_orders_by_name()
    {
        var store = new InMemoryCalendarPresenceStore();
        await store.AddAsync("c1", new CalendarPresenceDto("u2", "zoe@x"));
        await store.AddAsync("c2", new CalendarPresenceDto("u1", "anna@x"));
        await store.AddAsync("c3", new CalendarPresenceDto("u2", "zoe@x"));

        Assert.Equal(["anna@x", "zoe@x"], (await store.GetRosterAsync()).Select(p => p.Name));

        await store.RemoveAsync("c1");
        Assert.Equal(2, (await store.GetRosterAsync()).Count); // zoe still has a second tab
    }
}

/// <summary>
/// Runs against a real Redis named by <c>REDIS_TEST_CONNECTION</c> (CI provides a service container).
/// Without it the tests return early, so a plain <c>dotnet test</c> needs no Redis.
/// </summary>
public class RedisPresenceStoreTests : IAsyncLifetime
{
    private readonly string? _connection = Environment.GetEnvironmentVariable("REDIS_TEST_CONNECTION");
    private readonly string _prefix = $"test:{Guid.NewGuid():N}";
    private readonly ManualClock _clock = new(DateTimeOffset.Parse("2026-01-01T09:00:00Z"));
    private ConnectionMultiplexer? _redis;

    public async Task InitializeAsync()
    {
        if (_connection is not null)
        {
            _redis = await ConnectionMultiplexer.ConnectAsync(_connection);
        }
    }

    public async Task DisposeAsync()
    {
        if (_redis is not null)
        {
            await _redis.GetDatabase().KeyDeleteAsync([$"{_prefix}:members", $"{_prefix}:seen"]);
            await _redis.DisposeAsync();
        }
    }

    private RedisCalendarPresenceStore Instance() => new(_redis!, _clock, _prefix);

    [Fact]
    public async Task Two_instances_share_one_roster()
    {
        if (_redis is null) return;
        var a = Instance();
        var b = Instance();

        await a.AddAsync("ca", new CalendarPresenceDto("u1", "anna@x"));
        await b.AddAsync("cb", new CalendarPresenceDto("u2", "bruno@x"));
        await b.AddAsync("cb2", new CalendarPresenceDto("u1", "anna@x"));

        Assert.Equal(["anna@x", "bruno@x"], (await a.GetRosterAsync()).Select(p => p.Name));

        await b.RemoveAsync("cb");
        Assert.Equal(["anna@x"], (await a.GetRosterAsync()).Select(p => p.Name));
    }

    [Fact]
    public async Task Viewers_of_a_crashed_instance_expire_while_live_ones_are_kept_by_heartbeats()
    {
        if (_redis is null) return;
        var crashed = Instance();
        var alive = Instance();
        await crashed.AddAsync("dead", new CalendarPresenceDto("u1", "ghost@x"));
        await alive.AddAsync("live", new CalendarPresenceDto("u2", "real@x"));

        // Only the surviving instance keeps beating past the TTL.
        for (var i = 0; i < 4; i++)
        {
            _clock.Advance(PresenceHeartbeatService.Interval);
            await alive.HeartbeatAsync();
        }

        Assert.Equal(["real@x"], (await alive.GetRosterAsync()).Select(p => p.Name));
        Assert.False(await alive.HeartbeatAsync()); // already pruned — nothing more to drop
    }

    [Fact]
    public async Task Heartbeat_reports_when_it_pruned_stale_entries()
    {
        if (_redis is null) return;
        var crashed = Instance();
        var alive = Instance();
        await crashed.AddAsync("dead", new CalendarPresenceDto("u1", "ghost@x"));

        _clock.Advance(RedisCalendarPresenceStore.Ttl + TimeSpan.FromSeconds(1));

        Assert.True(await alive.HeartbeatAsync());
        Assert.Empty(await alive.GetRosterAsync());
    }

    private sealed class ManualClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now += by;
    }
}

/// <summary>End-to-end over the real hub: connecting users see each other, leaving drops them.</summary>
public class CalendarHubPresenceTests
{
    private static HubConnection Connect(ApiFactory factory, string role) =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, "hubs/installations"), options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.Headers[ApiFactory.RoleHeader] = role;
            })
            .Build();

    [Fact]
    public async Task Presence_roster_is_broadcast_on_connect_and_disconnect()
    {
        await using var factory = new ApiFactory();
        _ = factory.CreateClient(); // boot the server

        var rosters = new List<IReadOnlyList<CalendarPresenceDto>>();
        var gotRoster = new SemaphoreSlim(0);
        await using var watcher = Connect(factory, "Sales");
        watcher.On<IReadOnlyList<CalendarPresenceDto>>("PresenceChanged", r =>
        {
            lock (rosters) rosters.Add(r);
            gotRoster.Release();
        });

        await watcher.StartAsync();
        Assert.True(await gotRoster.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.Single(rosters[^1]);

        await using var unauthenticated = Connect(factory, "");
        await Assert.ThrowsAnyAsync<Exception>(() => unauthenticated.StartAsync());
    }
}

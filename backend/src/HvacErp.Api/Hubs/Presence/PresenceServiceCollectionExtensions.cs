using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;

namespace HvacErp.Api.Hubs.Presence;

/// <summary>Registers the calendar's SignalR scale-out: Redis backplane + shared presence when configured.</summary>
public static class PresenceServiceCollectionExtensions
{
    /// <summary>
    /// With <c>Redis:ConnectionString</c> set, SignalR fans out through the Redis backplane (so
    /// <c>Clients.All</c> reaches every instance) and presence is kept in Redis; otherwise both stay
    /// in-process, which is exact for the single-instance deployment.
    /// </summary>
    public static ISignalRServerBuilder AddCalendarScaleOut(this ISignalRServerBuilder signalR, IConfiguration configuration)
    {
        var services = signalR.Services;
        var redisConnection = configuration["Redis:ConnectionString"];

        if (string.IsNullOrWhiteSpace(redisConnection))
        {
            services.AddSingleton<ICalendarPresenceStore, InMemoryCalendarPresenceStore>();
            return signalR;
        }

        var options = ConfigurationOptions.Parse(redisConnection);
        options.AbortOnConnectFail = false; // start even if Redis is briefly down; it reconnects
        var multiplexer = ConnectionMultiplexer.Connect(options);

        services.AddSingleton<IConnectionMultiplexer>(multiplexer);
        services.AddSingleton<ICalendarPresenceStore>(sp =>
            new RedisCalendarPresenceStore(sp.GetRequiredService<IConnectionMultiplexer>(), TimeProvider.System));
        services.AddHostedService<PresenceHeartbeatService>();

        signalR.AddStackExchangeRedis(o =>
        {
            o.Configuration = options;
            o.Configuration.ChannelPrefix = RedisChannel.Literal("hvac-erp-signalr");
        });
        return signalR;
    }
}

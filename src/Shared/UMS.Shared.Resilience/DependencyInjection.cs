using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace UMS.Shared.Resilience;

/// <summary>
/// Registers the single <see cref="IConnectionMultiplexer"/> every module shares for caching, rate
/// limiting, and coordination (ADR-0007: "Redis is the only cache layer"). One connection, reused
/// platform-wide, rather than a multiplexer per module.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Connects <b>asynchronously</b>, once, at startup, and registers the already-connected
    /// instance - deliberately not the common `AddSingleton(sp => ConnectionMultiplexer.Connect(...))`
    /// pattern. That pattern's synchronous, blocking `Connect()` runs lazily on whatever thread
    /// first resolves it - typically inside a request pipeline mid-request - and sync-over-async
    /// blocking there is a well-documented source of thread-pool-starvation deadlocks/timeouts
    /// under any concurrent load on that same pool (confirmed directly in this module's own
    /// integration-test host, which hit exactly this: every first Redis-touching request timed
    /// out even though Redis itself was reachable). Connecting once with a real `await`, before
    /// the host starts serving traffic, has no such hazard.
    /// </summary>
    public static async Task<IServiceCollection> AddUmsResilienceAsync(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException("Missing required 'ConnectionStrings:Redis' configuration value.");

        var options = ConfigurationOptions.Parse(connectionString);

        // Defense in depth even with the async connect above: a connection dropped later (e.g. a
        // Redis failover) should make the multiplexer keep retrying in the background rather than
        // every subsequent command throwing outright (StackExchange.Redis's own guidance).
        options.AbortOnConnectFail = false;

        var multiplexer = await ConnectionMultiplexer.ConnectAsync(options).ConfigureAwait(false);
        services.AddSingleton<IConnectionMultiplexer>(multiplexer);

        return services;
    }
}

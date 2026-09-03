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
    public static IServiceCollection AddUmsResilience(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException("Missing required 'ConnectionStrings:Redis' configuration value.");

        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(connectionString));

        return services;
    }
}

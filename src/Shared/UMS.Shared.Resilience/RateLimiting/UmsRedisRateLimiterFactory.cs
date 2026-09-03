using System.Threading.RateLimiting;
using StackExchange.Redis;

namespace UMS.Shared.Resilience.RateLimiting;

/// <summary>
/// Builds a <see cref="PartitionedRateLimiter{T}"/> for ASP.NET Core's rate-limiting middleware,
/// keyed however the caller likes (client id, IP, user id) and backed by
/// <see cref="RedisFixedWindowRateLimiter"/> per partition. Every module registers its own
/// endpoint-specific limiter through this instead of hand-rolling one
/// (ums-conventions.md, Resilience & Reliability).
/// </summary>
public static class UmsRedisRateLimiterFactory
{
    public static PartitionedRateLimiter<T> Create<T>(
        IConnectionMultiplexer redis,
        string limiterName,
        Func<T, string> partitionKeySelector,
        int permitLimit,
        TimeSpan window)
    {
        return PartitionedRateLimiter.Create<T, string>(item =>
        {
            var partitionKey = partitionKeySelector(item);
            var redisKey = $"ratelimit:{limiterName}:{partitionKey}";

            return RateLimitPartition.Get(
                partitionKey,
                _ => new RedisFixedWindowRateLimiter(redis, redisKey, permitLimit, window));
        });
    }
}

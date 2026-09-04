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
        return PartitionedRateLimiter.Create<T, string>(item => CreatePartition(redis, limiterName, partitionKeySelector(item), permitLimit, window));
    }

    /// <summary>
    /// The single-partition building block <see cref="Create{T}"/> itself uses - exposed directly
    /// for ASP.NET Core's named-policy registration shape
    /// (<c>RateLimiterOptions.AddPolicy&lt;TPartitionKey&gt;(string, Func&lt;HttpContext,
    /// RateLimitPartition&lt;TPartitionKey&gt;&gt;)</c>), which needs one
    /// <see cref="RateLimitPartition{TKey}"/> resolved per request rather than a whole
    /// pre-built <see cref="PartitionedRateLimiter{T}"/> (Documents' public verify endpoint,
    /// DOC-9, is this method's first caller).
    /// </summary>
    public static RateLimitPartition<string> CreatePartition(IConnectionMultiplexer redis, string limiterName, string partitionKey, int permitLimit, TimeSpan window)
    {
        var redisKey = $"ratelimit:{limiterName}:{partitionKey}";
        return RateLimitPartition.Get(partitionKey, _ => new RedisFixedWindowRateLimiter(redis, redisKey, permitLimit, window));
    }
}

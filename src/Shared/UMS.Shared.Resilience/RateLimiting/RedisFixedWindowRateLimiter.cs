using System.Threading.RateLimiting;
using StackExchange.Redis;

namespace UMS.Shared.Resilience.RateLimiting;

/// <summary>
/// A fixed-window <see cref="RateLimiter"/> backed by Redis atomic counters (ums-conventions.md,
/// Resilience & Reliability: "Redis-backed specifically because ums-core runs N replicas and an
/// in-memory limiter would under-count across them"). One instance is created per partition key
/// (typically a client id/IP) by <see cref="UmsRedisRateLimiterFactory"/> - the Lua script keeps
/// the increment-and-set-expiry pair atomic so concurrent requests across replicas never
/// under-count or leave a key without a TTL.
/// </summary>
internal sealed class RedisFixedWindowRateLimiter(
    IConnectionMultiplexer redis,
    string redisKey,
    int permitLimit,
    TimeSpan window) : RateLimiter
{
    private const string IncrementAndExpireScript = """
        local current = redis.call('INCR', KEYS[1])
        if tonumber(current) == 1 then
            redis.call('PEXPIRE', KEYS[1], ARGV[1])
        end
        return current
        """;

    public override TimeSpan? IdleDuration => null;

    public override RateLimiterStatistics? GetStatistics() => null;

    protected override RateLimitLease AttemptAcquireCore(int permitCount)
    {
        var db = redis.GetDatabase();
        var result = (long)db.ScriptEvaluate(
            IncrementAndExpireScript,
            [(RedisKey)redisKey],
            [(RedisValue)(long)window.TotalMilliseconds]);

        return result <= permitLimit
            ? new RedisRateLimitLease(true, null)
            : new RedisRateLimitLease(false, window);
    }

    protected override async ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken cancellationToken)
    {
        var db = redis.GetDatabase();
        var result = (long)await db.ScriptEvaluateAsync(
            IncrementAndExpireScript,
            [(RedisKey)redisKey],
            [(RedisValue)(long)window.TotalMilliseconds]).ConfigureAwait(false);

        return result <= permitLimit
            ? new RedisRateLimitLease(true, null)
            : new RedisRateLimitLease(false, window);
    }
}

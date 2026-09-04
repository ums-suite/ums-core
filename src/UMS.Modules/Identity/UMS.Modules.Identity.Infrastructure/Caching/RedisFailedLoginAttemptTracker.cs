using Microsoft.Extensions.Options;
using StackExchange.Redis;
using UMS.Modules.Identity.Application;
using UMS.Modules.Identity.Application.Abstractions;

namespace UMS.Modules.Identity.Infrastructure.Caching;

/// <summary>
/// IDN-17/design-decisions.md, "Rate-Limiting / Lockout Mechanism": an atomic Redis
/// <c>INCR</c>+<c>EXPIRE</c> counter per login identifier - edge-cases.md's "Concurrent login
/// attempts triggering lockout vs. legitimate retry race" chose this specifically over a
/// read-then-write counter, and specifically over a row lock, so a burst of concurrent attempts
/// against one identifier can never under-count. <c>EXPIRE</c> only applies the very first time a
/// window's key is created (<c>StringIncrementAsync</c> returning 1) - every later increment in the
/// same window leaves the existing TTL alone, which is what makes this a fixed window rather than a
/// TTL that keeps sliding forward on every attempt.
/// </summary>
internal sealed class RedisFailedLoginAttemptTracker(IConnectionMultiplexer redis, IOptions<IdentityLockoutOptions> options) : IFailedLoginAttemptTracker
{
    public async Task<int> RegisterFailureAsync(string identifier, CancellationToken cancellationToken = default)
    {
        var db = redis.GetDatabase();
        var key = KeyFor(identifier);

        var count = await db.StringIncrementAsync(key).ConfigureAwait(false);
        if (count == 1)
        {
            await db.KeyExpireAsync(key, options.Value.Window).ConfigureAwait(false);
        }

        return (int)count;
    }

    public Task ResetAsync(string identifier, CancellationToken cancellationToken = default) =>
        redis.GetDatabase().KeyDeleteAsync(KeyFor(identifier));

    private static string KeyFor(string identifier) => $"identity:login-failures:{identifier.Trim().ToLowerInvariant()}";
}

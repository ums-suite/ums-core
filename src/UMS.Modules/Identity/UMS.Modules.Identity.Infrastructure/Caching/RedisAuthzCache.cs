using StackExchange.Redis;
using UMS.Modules.Identity.Application.Abstractions;
using UMS.Modules.Identity.Domain.Sessions;
using UMS.Modules.Identity.Domain.Users;

namespace UMS.Modules.Identity.Infrastructure.Caching;

/// <summary>
/// Write side of the shared permission-check cache (design-decisions.md, "Permission-Check
/// Caching vs. Live Lookup"). Deleting the snapshot key (rather than writing a new one) is
/// intentional - the very next permission check simply recomputes it from Postgres on a cache
/// miss, so there is never a window where a stale snapshot outlives the event that invalidated it.
/// </summary>
internal sealed class RedisAuthzCache(IConnectionMultiplexer redis) : IAuthzCache
{
    public Task InvalidateUserAsync(UserId userId, CancellationToken cancellationToken = default) =>
        redis.GetDatabase().KeyDeleteAsync(AuthzSnapshotKey(userId));

    public Task RevokeSessionAsync(SessionId sessionId, TimeSpan ttl, CancellationToken cancellationToken = default) =>
        redis.GetDatabase().StringSetAsync(SessionRevokedKey(sessionId), "1", ttl);

    internal static string AuthzSnapshotKey(UserId userId) => $"identity:authz:{userId.Value:N}";

    internal static string SessionRevokedKey(SessionId sessionId) => $"identity:session-revoked:{sessionId.Value:N}";
}

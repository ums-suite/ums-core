using StackExchange.Redis;
using UMS.Modules.Organization.Application.Abstractions;

namespace UMS.Modules.Organization.Infrastructure.Caching;

/// <summary>
/// design-decisions.md, "Caching Strategy for the Hierarchy Tree": cache-aside Redis cache for
/// `GET /organization-tree` and `GET /nodes/{id}/ancestors`, explicit targeted invalidation on
/// every structural write, short TTL as a backstop. Shares the platform's one
/// <see cref="IConnectionMultiplexer"/> (ADR-0007) - never a second multiplexer - exactly like
/// Identity's own <c>RedisAuthzCache</c>.
/// </summary>
internal sealed class RedisOrganizationTreeCache(IConnectionMultiplexer redis) : IOrganizationTreeCache
{
    /// <summary>Minutes, not seconds (design-decisions.md's own phrasing) - a backstop against a missed invalidation, not the primary freshness mechanism.</summary>
    private static readonly TimeSpan _ttl = TimeSpan.FromMinutes(5);

    public async Task<string?> GetTreeJsonAsync(Guid? rootId, CancellationToken cancellationToken = default)
    {
        var value = await redis.GetDatabase().StringGetAsync(TreeKey(rootId)).ConfigureAwait(false);
        return value.HasValue ? value.ToString() : null;
    }

    public Task SetTreeJsonAsync(Guid? rootId, string json, CancellationToken cancellationToken = default) =>
        redis.GetDatabase().StringSetAsync(TreeKey(rootId), json, _ttl);

    public async Task<string?> GetAncestorsJsonAsync(Guid nodeId, CancellationToken cancellationToken = default)
    {
        var value = await redis.GetDatabase().StringGetAsync(AncestorsKey(nodeId)).ConfigureAwait(false);
        return value.HasValue ? value.ToString() : null;
    }

    public Task SetAncestorsJsonAsync(Guid nodeId, string json, CancellationToken cancellationToken = default) =>
        redis.GetDatabase().StringSetAsync(AncestorsKey(nodeId), json, _ttl);

    public Task InvalidateAsync(
        Guid mutatedNodeId,
        IReadOnlyCollection<Guid> ancestorIds,
        IReadOnlyCollection<Guid>? descendantIds = null,
        CancellationToken cancellationToken = default)
    {
        var keys = new List<RedisKey> { TreeKey(null), TreeKey(mutatedNodeId), AncestorsKey(mutatedNodeId) };
        keys.AddRange(ancestorIds.Select(id => (RedisKey)TreeKey(id)));

        if (descendantIds is { Count: > 0 })
        {
            keys.AddRange(descendantIds.Select(id => (RedisKey)AncestorsKey(id)));
        }

        return redis.GetDatabase().KeyDeleteAsync(keys.ToArray());
    }

    private static string TreeKey(Guid? rootId) => rootId is null ? "organization:tree:full" : $"organization:tree:{rootId.Value:N}";

    private static string AncestorsKey(Guid nodeId) => $"organization:ancestors:{nodeId:N}";
}

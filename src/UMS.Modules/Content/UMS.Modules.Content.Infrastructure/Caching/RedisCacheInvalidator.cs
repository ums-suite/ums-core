using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using UMS.Modules.Content.Application.Abstractions;

namespace UMS.Modules.Content.Infrastructure.Caching;

/// <summary>
/// design-decisions.md "Cache-Correctness Backstop": a best-effort Redis <c>DEL</c> against the
/// existing <c>ums-redis</c> instance (ADR-0007's one shared multiplexer), logged-and-continued on
/// failure - NEVER rethrown, NEVER retried inline, NEVER couples a mutation's own transaction
/// success to this call's outcome. The real correctness backstop lives in
/// <c>NoticeService.GetByIdAsync</c>'s own Archived check, not here.
/// </summary>
internal sealed class RedisCacheInvalidator(IConnectionMultiplexer redis, ILogger<RedisCacheInvalidator> logger) : ICacheInvalidator
{
    public async Task InvalidateAsync(string cacheKey, CancellationToken cancellationToken = default)
    {
        try
        {
            await redis.GetDatabase().KeyDeleteAsync(cacheKey).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // edge-cases.md "CDN/Redis cache for a just-Archived notice is not invalidated in time":
            // a failed purge is an ops-visible, non-paging concern (Content's lower-alerting tier) -
            // never a reason to fail or roll back the mutation that already committed.
            logger.LogWarning(ex, "Content cache invalidation failed for key {CacheKey} - a stale entry may briefly remain; origin reads remain correct regardless.", cacheKey);
        }
    }
}

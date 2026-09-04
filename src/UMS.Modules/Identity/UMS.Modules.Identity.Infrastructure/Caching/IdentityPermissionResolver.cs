using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using UMS.Modules.Identity.Domain.Users;
using UMS.Modules.Identity.Infrastructure.Persistence;
using UMS.Shared.Authorization;

namespace UMS.Modules.Identity.Infrastructure.Caching;

/// <summary>
/// IDN-4's read side: resolves "does this User currently hold this Permission" via a cache-aside
/// Redis snapshot with a short TTL, falling back to Postgres on a miss
/// (design-decisions.md, "Permission-Check Caching vs. Live Lookup"). The session-revocation check
/// runs first and separately, as a cheap fast-path negative check (edge-cases.md, "'Log out
/// everywhere' racing an in-flight request").
/// </summary>
internal sealed class IdentityPermissionResolver(IdentityDbContext context, IConnectionMultiplexer redis) : IPermissionResolver
{
    private static readonly TimeSpan _snapshotTtl = TimeSpan.FromSeconds(45);

    public async Task<PermissionCheckOutcome> CheckAsync(Guid userId, Guid sessionId, string permission, CancellationToken cancellationToken = default)
    {
        var (livenessOutcome, snapshot) = await CheckLivenessCoreAsync(userId, sessionId, cancellationToken).ConfigureAwait(false);
        if (livenessOutcome != PermissionCheckOutcome.Granted)
        {
            return livenessOutcome;
        }

        return Array.IndexOf(snapshot!.Permissions, permission) >= 0
            ? PermissionCheckOutcome.Granted
            : PermissionCheckOutcome.PermissionDenied;
    }

    public async Task<PermissionCheckOutcome> CheckLivenessAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        var (outcome, _) = await CheckLivenessCoreAsync(userId, sessionId, cancellationToken).ConfigureAwait(false);
        return outcome;
    }

    private async Task<(PermissionCheckOutcome Outcome, AuthzSnapshot? Snapshot)> CheckLivenessCoreAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken)
    {
        var db = redis.GetDatabase();

        if (await db.KeyExistsAsync(RedisAuthzCache.SessionRevokedKey(new Domain.Sessions.SessionId(sessionId))).ConfigureAwait(false))
        {
            return (PermissionCheckOutcome.SessionRevoked, null);
        }

        var snapshot = await GetOrLoadSnapshotAsync(new UserId(userId), db, cancellationToken).ConfigureAwait(false);
        if (snapshot is null)
        {
            return (PermissionCheckOutcome.UserNotFound, null);
        }

        return snapshot.Active
            ? (PermissionCheckOutcome.Granted, snapshot)
            : (PermissionCheckOutcome.UserInactive, null);
    }

    private async Task<AuthzSnapshot?> GetOrLoadSnapshotAsync(UserId userId, IDatabase db, CancellationToken cancellationToken)
    {
        var key = RedisAuthzCache.AuthzSnapshotKey(userId);
        var cached = await db.StringGetAsync(key).ConfigureAwait(false);
        if (cached.HasValue)
        {
            return JsonSerializer.Deserialize<AuthzSnapshot>((string)cached!);
        }

        var user = await context.Users
            .AsNoTracking()
            .Include(u => u.RoleAssignments)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            .ConfigureAwait(false);

        if (user is null)
        {
            return null;
        }

        var activeRoleIds = user.RoleAssignments.Where(a => a.IsActive).Select(a => a.RoleId).Distinct().ToList();
        var roles = activeRoleIds.Count == 0
            ? []
            : await context.Roles.AsNoTracking().Where(r => activeRoleIds.Contains(r.Id)).ToListAsync(cancellationToken).ConfigureAwait(false);

        var permissions = roles.SelectMany(r => r.Permissions).Distinct(StringComparer.Ordinal).ToArray();
        var snapshot = new AuthzSnapshot(user.Status == UserStatus.Active, permissions);

        await db.StringSetAsync(key, JsonSerializer.Serialize(snapshot), _snapshotTtl).ConfigureAwait(false);
        return snapshot;
    }

    private sealed record AuthzSnapshot(bool Active, string[] Permissions);
}

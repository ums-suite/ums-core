using Microsoft.EntityFrameworkCore;
using UMS.Modules.Identity.Domain.ScopeGrants;
using UMS.Modules.Identity.Domain.Users;
using UMS.Modules.Identity.Infrastructure.Persistence;
using UMS.Shared.Identity;

namespace UMS.Modules.Identity.Infrastructure.CrossModule;

/// <summary>The one real implementation of <see cref="IScopeGrantDirectory"/> - see that interface's own remarks. Reads live from Postgres (not the Redis authz snapshot <c>IdentityPermissionResolver</c> uses) since a scope-directory lookup is a low-frequency, cross-module administrative query (STU-11 submission/review), not a per-request hot path needing the same 45s-TTL caching.</summary>
internal sealed class ScopeGrantDirectory(IdentityDbContext context) : IScopeGrantDirectory
{
    public async Task<bool> HasPermissionAtScopeAsync(Guid userId, string permission, Guid organizationNodeId, CancellationToken cancellationToken = default)
    {
        var holderIds = await GetUserIdsWithPermissionAtScopeAsync(permission, organizationNodeId, cancellationToken).ConfigureAwait(false);
        return holderIds.Contains(userId);
    }

    public async Task<IReadOnlyCollection<Guid>> GetUserIdsWithPermissionAtScopeAsync(string permission, Guid organizationNodeId, CancellationToken cancellationToken = default)
    {
        var scopeNode = new OrganizationNodeId(organizationNodeId);

        var users = await context.Users
            .AsNoTracking()
            .Where(u => u.Status == UserStatus.Active)
            .Include(u => u.RoleAssignments)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var candidateRoleIds = users
            .SelectMany(u => u.RoleAssignments)
            .Where(a => a.IsActive && (a.ScopeNode == null || a.ScopeNode == scopeNode))
            .Select(a => a.RoleId)
            .Distinct()
            .ToList();

        if (candidateRoleIds.Count == 0)
        {
            return [];
        }

        var grantingRoleIds = await context.Roles
            .AsNoTracking()
            .Where(r => candidateRoleIds.Contains(r.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var grantingRoleIdSet = grantingRoleIds.Where(r => r.Permissions.Contains(permission, StringComparer.Ordinal)).Select(r => r.Id).ToHashSet();

        return users
            .Where(u => u.RoleAssignments.Any(a => a.IsActive && grantingRoleIdSet.Contains(a.RoleId) && (a.ScopeNode == null || a.ScopeNode == scopeNode)))
            .Select(u => u.Id.Value)
            .Distinct()
            .ToList();
    }
}

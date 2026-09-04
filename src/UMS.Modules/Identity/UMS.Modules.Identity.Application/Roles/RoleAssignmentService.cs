using UMS.Modules.Identity.Application.Abstractions;
using UMS.Modules.Identity.Domain.Roles;
using UMS.Modules.Identity.Domain.ScopeGrants;
using UMS.Modules.Identity.Domain.Users;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Identity.Application.Roles;

/// <summary>
/// IDN-15: assigns a Role (with an optional ScopeGrant) to a User, resolving the
/// <c>OrganizationNode</c> reference via Organization's own read interface
/// (requirement-spec.md identity §2/§3/§6/§7; edge-cases.md, "Role revoked mid-session" is the
/// revoke half of this same service).
/// </summary>
public sealed class RoleAssignmentService(
    IUserRepository users,
    IRoleRepository roles,
    IOrganizationNodeExistenceChecker organizationNodes,
    IUnitOfWork unitOfWork,
    IAuthzCache authzCache,
    IClock clock)
{
    public async Task<Result<UserRoleAssignmentDto>> AssignAsync(Guid userId, AssignRoleRequest request, CancellationToken cancellationToken = default)
    {
        var user = await users.GetByIdAsync(new UserId(userId), cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return Error.NotFound("user.not_found", $"No User exists with id '{userId}'.");
        }

        var role = await roles.GetByIdAsync(new RoleId(request.RoleId), cancellationToken).ConfigureAwait(false);
        if (role is null)
        {
            return Error.NotFound("role.not_found", $"No Role exists with id '{request.RoleId}'.");
        }

        OrganizationNodeId? scopeNode = null;
        if (request.OrganizationNodeId is { } rawNodeId)
        {
            scopeNode = new OrganizationNodeId(rawNodeId);
            if (!await organizationNodes.ExistsAsync(scopeNode.Value, cancellationToken).ConfigureAwait(false))
            {
                return Error.Validation("role.scope_grant_invalid_organization_node", $"OrganizationNode '{rawNodeId}' does not exist.");
            }
        }

        UserRoleAssignment assignment;
        try
        {
            assignment = user.AssignRole(role.Id, scopeNode, clock.UtcNow);
        }
        catch (InvalidOperationException ex)
        {
            return Error.Conflict("role.already_assigned", ex.Message);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await authzCache.InvalidateUserAsync(user.Id, cancellationToken).ConfigureAwait(false);

        return new UserRoleAssignmentDto(assignment.Id.Value, role.Id.Value, role.Name, scopeNode?.Value, assignment.AssignedAt);
    }

    public async Task<Result> RevokeAsync(Guid userId, Guid assignmentId, CancellationToken cancellationToken = default)
    {
        var user = await users.GetByIdAsync(new UserId(userId), cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return Result.Failure(Error.NotFound("user.not_found", $"No User exists with id '{userId}'."));
        }

        try
        {
            user.RevokeRole(new UserRoleAssignmentId(assignmentId), clock.UtcNow);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(Error.Conflict("role.assignment_not_revocable", ex.Message));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // "Role revoked mid-session" (edge-cases.md): the affected User's next permission check
        // must reflect the revoke, not the cache's stale snapshot for up to its full TTL.
        await authzCache.InvalidateUserAsync(user.Id, cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}

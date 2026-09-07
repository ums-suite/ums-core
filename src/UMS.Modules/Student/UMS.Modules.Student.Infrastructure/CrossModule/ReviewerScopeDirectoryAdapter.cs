using UMS.Modules.Student.Application.Abstractions;
using UMS.Shared.Identity;

namespace UMS.Modules.Student.Infrastructure.CrossModule;

/// <summary>STU-11: adapts <c>UMS.Shared.Identity.IScopeGrantDirectory</c> to Student's own local port.</summary>
internal sealed class ReviewerScopeDirectoryAdapter(IScopeGrantDirectory scopeGrantDirectory) : IReviewerScopeDirectory
{
    public Task<bool> HasPermissionAtScopeAsync(Guid userId, string permission, Guid organizationNodeId, CancellationToken cancellationToken = default) =>
        scopeGrantDirectory.HasPermissionAtScopeAsync(userId, permission, organizationNodeId, cancellationToken);

    public Task<IReadOnlyCollection<Guid>> GetUserIdsWithPermissionAtScopeAsync(string permission, Guid organizationNodeId, CancellationToken cancellationToken = default) =>
        scopeGrantDirectory.GetUserIdsWithPermissionAtScopeAsync(permission, organizationNodeId, cancellationToken);
}

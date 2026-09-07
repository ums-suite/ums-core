namespace UMS.Modules.Student.Application.Abstractions;

/// <summary>STU-11: Student's own local port onto <c>UMS.Shared.Identity.IScopeGrantDirectory</c> - the ADR-0006 ScopeGrant gate behind grievance routing/reviewer-visibility.</summary>
public interface IReviewerScopeDirectory
{
    public Task<bool> HasPermissionAtScopeAsync(Guid userId, string permission, Guid organizationNodeId, CancellationToken cancellationToken = default);

    public Task<IReadOnlyCollection<Guid>> GetUserIdsWithPermissionAtScopeAsync(string permission, Guid organizationNodeId, CancellationToken cancellationToken = default);
}

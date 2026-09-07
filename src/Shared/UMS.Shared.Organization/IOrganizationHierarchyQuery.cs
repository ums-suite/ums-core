namespace UMS.Shared.Organization;

/// <summary>
/// STU-11: the one-level hierarchy walk-up Student's grievance-routing escalation needs
/// (edge-cases.md, "A grievance is filed against a Department Head's own department" - "escalates
/// to the next scope level (Faculty ...)"). Deliberately narrower than a general
/// ancestor-path/tree-walk API: Student's own escalation rule only ever needs "the Department's
/// parent Faculty," never a full path, so this contract names exactly that rather than a generic
/// walk a caller would have to interpret.
///
/// <para>
/// Living in <c>UMS.Shared.Organization</c> - not <c>UMS.Modules.Organization.*</c> - is what lets
/// Student call it without a forbidden dependency on Organization's Domain/Application/
/// Infrastructure internals (module-boundaries.md, ADR-0002), mirroring
/// <see cref="IOrganizationNodeExistenceChecker"/>'s own exact pattern - Organization's own
/// Infrastructure layer registers the one real implementation directly.
/// </para>
/// </summary>
public interface IOrganizationHierarchyQuery
{
    /// <summary><see langword="null"/> if <paramref name="departmentId"/> does not resolve to an existing Department - Student's own caller treats that the same as "no parent to escalate to" (falls back to the bare, university-wide `studentrequest.review` permission per <c>StudentRequest.ReviewScopeNodeId</c>'s own remarks).</summary>
    public Task<Guid?> GetParentFacultyIdAsync(Guid departmentId, CancellationToken cancellationToken = default);
}

namespace UMS.Modules.Organization.Application.Abstractions;

/// <summary>
/// edge-cases.md "Deactivating a Department that still has a Faculty member on record": resolves
/// whether the `Faculty` module (release/DEVELOPMENT_PLAN.md Flow #10 - does not exist yet) still
/// has an active `FacultyMember` attached to a given Department, via Faculty's own read interface
/// (ADR-0002: "no module may assume another module's data shape ... a live call, not a cached
/// assumption"). Until Flow #10 lands, the one Infrastructure registration against this interface
/// is an explicit stub seam (<c>StubFacultyEmploymentChecker</c>) - see that class's own remarks,
/// mirroring exactly how Identity's own <c>IOrganizationNodeExistenceChecker</c> was stubbed until
/// this module (Flow #6) existed to implement it for real.
/// </summary>
public interface IFacultyEmploymentChecker
{
    /// <summary>True if Faculty reports at least one active FacultyMember still attached to <paramref name="departmentId"/> - a true result blocks the Department's deactivation (requirement-spec.md organization §8).</summary>
    public Task<bool> HasActiveFacultyMemberAsync(Guid departmentId, CancellationToken cancellationToken = default);
}

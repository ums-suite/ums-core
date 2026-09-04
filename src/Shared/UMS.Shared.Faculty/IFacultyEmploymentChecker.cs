namespace UMS.Shared.Faculty;

/// <summary>
/// Promoted from Organization's own stub port (<c>Organization.Application.Abstractions.
/// IFacultyEmploymentChecker</c>, backed by <c>StubFacultyEmploymentChecker</c> until now) to a
/// real shared contract, now that Faculty (release/DEVELOPMENT_PLAN.md Flow #10) exists to
/// implement it - the exact arc <c>UMS.Shared.Organization.IOrganizationNodeExistenceChecker</c>'s
/// own doc comment describes for Identity's former stub. Organization's own Infrastructure now
/// registers a Pattern-B adapter (<c>FacultyEmploymentCheckerAdapter</c>) delegating to this
/// interface instead of the stub (edge-cases.md organization "Deactivating a Department that still
/// has a Faculty member on record").
/// </summary>
public interface IFacultyEmploymentChecker
{
    /// <summary>True if Faculty reports at least one <c>Active</c> FacultyMember still attached to <paramref name="departmentId"/> - a true result blocks the Department's deactivation.</summary>
    public Task<bool> HasActiveFacultyMemberAsync(Guid departmentId, CancellationToken cancellationToken = default);
}

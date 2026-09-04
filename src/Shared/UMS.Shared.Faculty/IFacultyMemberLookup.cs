namespace UMS.Shared.Faculty;

/// <summary>
/// FAC-3: the `GetFacultyMember` read-only query (release/DEVELOPMENT_PLAN.md Flow #10;
/// requirement-spec.md faculty §2 Course Assignment, §7 "Called by: Academic") - Academic (Flow
/// #12, does not exist yet) will call this before finalizing an instructor assignment, to validate
/// eligibility/timetable-overlap. Living in <c>UMS.Shared.Faculty</c> - not
/// <c>UMS.Modules.Faculty.*</c> - lets Academic call it without a forbidden dependency on Faculty's
/// Domain/Application/Infrastructure internals (module-boundaries.md, ADR-0002), mirroring
/// <c>UMS.Shared.Organization.IOrganizationNodeExistenceChecker</c>'s exact pattern. Faculty's own
/// Infrastructure layer registers the one real implementation here directly (no stub-then-promote
/// dance needed - Faculty is the first mover for this contract, unlike
/// <see cref="IFacultyEmploymentChecker"/> below, which is a promotion of an already-stubbed
/// Organization-side port).
/// </summary>
public interface IFacultyMemberLookup
{
    public Task<FacultyMemberSummary?> GetAsync(Guid facultyMemberId, CancellationToken cancellationToken = default);
}

/// <summary>The subset of a FacultyMember's employment profile Academic needs to validate an instructor assignment.</summary>
/// <param name="Id">The FacultyMember id.</param>
/// <param name="DepartmentId">Organization Department reference - Academic's own eligibility check compares this against the CourseOffering's owning Department.</param>
/// <param name="Status">`Active`/`OnLeave`/`Suspended`/`Separated` - Academic should reject assigning an instructor who isn't `Active`.</param>
public sealed record FacultyMemberSummary(Guid Id, Guid DepartmentId, string Status);

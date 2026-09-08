namespace UMS.Modules.Research.Domain.InstitutionalRepositoryEntries;

/// <summary>
/// requirement-spec.md §3/§9: <c>(name, facultyMemberId: nullable)</c> - the SAME mixed-reference
/// shape as <c>Publications.AuthorEntry</c>, deliberately (design-decisions.md "Mixed
/// Internal/External Authorship Modeling"). For a thesis/dissertation depositor,
/// <see cref="FacultyMemberId"/> is populated only for the supervising advisor (always a real
/// FacultyMember) - a graduate-student depositor is captured as plain <see cref="Name"/> text with
/// no system identity reference, specifically to avoid a <c>Research -&gt; Student</c> dependency
/// module-boundaries.md's Research row does not permit.
/// </summary>
public sealed record Contributor(string Name, Guid? FacultyMemberId);

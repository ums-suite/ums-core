namespace UMS.Shared.Student;

/// <summary>
/// ACD-6: the read-only lookup Academic (release/DEVELOPMENT_PLAN.md Flow #12; requirement-spec.md
/// academic §7 "Academic depends on ... Student, validating a Student's status is Active before
/// permitting Enrollment") needs before creating an <c>Enrollment</c> - a Suspended Student must
/// not be able to register (edge-cases.md academic, "A Student's status changes to Suspended
/// mid-registration-window").
///
/// <para>
/// Living in <c>UMS.Shared.Student</c> - not <c>UMS.Modules.Student.*</c> - lets Academic call it
/// without a forbidden dependency on Student's Domain/Application/Infrastructure internals
/// (module-boundaries.md, ADR-0002), mirroring <c>UMS.Shared.Faculty.IFacultyMemberLookup</c>'s
/// exact "first mover, no stub-then-promote dance needed" pattern - Student is the first mover for
/// this particular outward-facing contract (its <see cref="IStudentRecordProvisioner"/> sibling
/// already established the inbound-creation half; this is the outbound-query half a second real
/// caller, Academic, now needs). Student's own Infrastructure layer registers the one real
/// implementation directly.
/// </para>
///
/// <para>
/// <b>Deliberately NOT the reverse.</b> module-boundaries.md's dependency table states Student
/// depends on Identity/Organization/Admission only - never Academic - so Student's own
/// <c>ProgramId</c> reference (validated today via <c>StubProgramExistenceChecker</c>, a
/// deliberately permissive first-pass stub) is NOT promoted to a real call into Academic as part
/// of this module's build: doing so would create Academic&#8594;Student&#8594;Academic, a two-module
/// cycle ADR-0002's acyclic-graph requirement forbids outright, the same class of cycle
/// <c>module-boundaries.md</c>'s "Resolved Edge Case: Attendance (Academic &#8596; Faculty)" entry
/// already had to design around for a structurally identical reason. That stub is left exactly as
/// Student's own build left it - a documented, permanent (for v1) first-pass gap, not a
/// regression introduced here.
/// </para>
/// </summary>
public interface IStudentStatusChecker
{
    /// <summary>Resolves a Student by their own id.</summary>
    public Task<StudentAcademicStanding?> GetByStudentIdAsync(Guid studentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves a Student by the Identity <c>User</c> id backing their login - what Academic's
    /// own "Student-owned" endpoints (requirement-spec.md §6 <c>POST /enrollments</c>,
    /// <c>DELETE /enrollments/{id}</c>) use to turn the caller's JWT identity into the
    /// <c>StudentId</c> the request must be scoped to, without Academic needing its own copy of
    /// that mapping.
    /// </summary>
    public Task<StudentAcademicStanding?> GetByUserIdAsync(Guid identityUserId, CancellationToken cancellationToken = default);
}

/// <summary>The subset of a Student's own record Academic needs to gate/scope Enrollment.</summary>
/// <param name="StudentId">The Student id.</param>
/// <param name="DepartmentId">Organization Department reference.</param>
/// <param name="ProgramId">Academic Program reference - Academic's own credit-limit gate resolves the Program's configured maximum against this.</param>
/// <param name="Status">`Enrolled`/`Active`/`Graduated`/`Suspended`/`Transferred` - Academic accepts a new Enrollment only while this is `Active`.</param>
public sealed record StudentAcademicStanding(Guid StudentId, Guid DepartmentId, Guid ProgramId, string Status);

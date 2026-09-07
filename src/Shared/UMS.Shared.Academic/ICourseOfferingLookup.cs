namespace UMS.Shared.Academic;

/// <summary>
/// LRN-1 (release/DEVELOPMENT_PLAN.md Flow #13): the read-only <c>CourseOffering</c> query surface
/// Learning resolves everything cross-module through - offering existence, instructor identity, and
/// enrollment membership.
///
/// <para>
/// <b>Why instructor identity is resolved HERE and not against Faculty:</b> learning
/// requirement-spec.md §2/§9.1 and module-boundaries.md's dependency table both state Learning's
/// dependencies as Identity, Organization, Academic, Documents, Notifications - Faculty is
/// deliberately absent. Academic already depends on Faculty for exactly this lookup
/// (<c>CourseAssignment</c>/<c>IFacultyMemberLookup</c>), so Academic - not Learning - does the
/// Identity-user-to-FacultyMember resolution and hands Learning back a plain verdict. A second,
/// parallel Learning&#8594;Faculty edge would be redundant and would risk two modules independently
/// resolving "who teaches this offering" against different staleness windows.
/// </para>
///
/// <para>
/// Same reasoning applies to <see cref="GetEnrolledStudentAsync"/>: Academic owns
/// <c>Enrollment</c> and already depends on Student for the Identity-user-to-<c>StudentId</c>
/// mapping (<c>UMS.Shared.Student.IStudentStatusChecker</c>), so a Learning caller never needs a
/// Student dependency of its own to scope a Student-owned action.
/// </para>
///
/// <para>
/// Living in <c>UMS.Shared.Academic</c> - not <c>UMS.Modules.Academic.*</c> - is what lets Learning
/// call it without a forbidden dependency on Academic's Domain/Application/Infrastructure internals
/// (module-boundaries.md, ADR-0002), mirroring <c>UMS.Shared.Faculty.IFacultyMemberLookup</c>'s and
/// <c>UMS.Shared.Student.IStudentStatusChecker</c>'s exact "first mover, no stub-then-promote dance
/// needed" pattern. Academic's own Infrastructure layer registers the one real implementation.
/// </para>
///
/// <para>
/// <b>Strictly read-only, and strictly one-directional.</b> There is deliberately no write method
/// anywhere on this interface: learning design-decisions.md's "Cross-Module Feed of Assignment
/// Scores into Academic's Grade" resolves that Learning publishes <c>SubmissionEvaluated</c> as a
/// fan-out event and never calls Academic to mutate a <c>Grade</c>/<c>Assessment</c>. That
/// invariant is enforced structurally, by this contract's shape, not just by convention.
/// </para>
/// </summary>
public interface ICourseOfferingLookup
{
    /// <summary>Resolves a <c>CourseOffering</c> by id, or <see langword="null"/> if no such offering exists.</summary>
    public Task<CourseOfferingSummary?> GetAsync(Guid courseOfferingId, CancellationToken cancellationToken = default);

    /// <summary>
    /// learning requirement-spec.md §6's Instructor resource-ownership check: is the Identity
    /// <c>User</c> behind this request the currently-assigned, <c>Active</c> Instructor for this
    /// <c>CourseOffering</c>? A fresh, synchronous resolution on every call - never a cached or
    /// projected value, matching Academic's own attendance-permission gate exactly.
    /// </summary>
    public Task<bool> IsInstructorForOfferingAsync(Guid courseOfferingId, Guid identityUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the Identity <c>User</c> behind this request to their own non-dropped
    /// <c>Enrollment</c> in this <c>CourseOffering</c>, or <see langword="null"/> if they have
    /// none - the single call that both answers "may this caller see/act on this offering's
    /// Learning content?" and yields the <c>StudentId</c> a <c>Submission</c>/
    /// <c>SubmissionExtension</c> is keyed by.
    /// </summary>
    public Task<EnrolledStudentSummary?> GetEnrolledStudentAsync(Guid courseOfferingId, Guid identityUserId, CancellationToken cancellationToken = default);

    /// <summary>The same lookup keyed by the Student's own id rather than their Identity user id - what an Instructor granting a <c>SubmissionExtension</c> for a NAMED Student is checked against, and how the resulting fan-out resolves that Student's notification recipient.</summary>
    public Task<EnrolledStudentSummary?> GetEnrolledStudentByStudentIdAsync(Guid courseOfferingId, Guid studentId, CancellationToken cancellationToken = default);
}

/// <summary>The subset of a <c>CourseOffering</c> a caller outside Academic needs. <paramref name="InstructorFacultyMemberId"/> is <see langword="null"/> until an Instructor is assigned.</summary>
public sealed record CourseOfferingSummary(Guid Id, Guid CourseId, Guid SemesterId, Guid DepartmentId, Guid? InstructorFacultyMemberId);

/// <summary>One Student's own membership in a <c>CourseOffering</c>.</summary>
/// <param name="StudentId">The Student's own id.</param>
/// <param name="EnrollmentId">Academic's own <c>Enrollment</c> id for this membership.</param>
/// <param name="Status">Academic's own <c>EnrollmentStatus</c> name (<c>Pending</c>/<c>Active</c>/<c>Completed</c>; never <c>Dropped</c> - a dropped enrollment resolves as no membership at all).</param>
/// <param name="IdentityUserId">The Identity <c>User</c> backing this Student's login, or <see langword="null"/> if provisioning never completed (STU-2's user provisioning is best-effort).</param>
public sealed record EnrolledStudentSummary(Guid StudentId, Guid EnrollmentId, string Status, Guid? IdentityUserId);

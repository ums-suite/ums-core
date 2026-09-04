using UMS.Shared.ErrorHandling.Results;

namespace UMS.Shared.Student;

/// <summary>
/// STU-1: the ONLY way another module creates a <c>Student</c> record (requirement-spec.md student
/// §2 "Applicant → Student Creation Handoff"; §6: "<c>POST /students</c> (internal only) ...
/// called from Admission's application-service interface, not exposed as a public HTTP route").
///
/// <para>
/// Living in <c>UMS.Shared.Student</c> - not <c>UMS.Modules.Student.*</c> - is what lets Admission
/// (release/DEVELOPMENT_PLAN.md Flow #15, not yet built) call it without a forbidden dependency on
/// Student's Domain/Application/Infrastructure internals (module-boundaries.md, ADR-0002),
/// mirroring <c>UMS.Shared.Faculty.IFacultyMemberLookup</c>'s exact "first mover, no stub-then-
/// promote dance needed" pattern - Student is the first mover for this contract, the same way
/// Faculty was the first mover for its own outward-facing shared interfaces.
/// </para>
///
/// <para>
/// <b>Known gap, documented rather than glossed over:</b> Admission (Flow #15) does not exist in
/// this build (release/DEVELOPMENT_PLAN.md row 11: "Student — Core ... no live Admission
/// integration yet"). This contract, its idempotency mechanism (design-decisions.md, "Idempotency
/// Mechanism for CreateStudentRecord"), and its downstream side effects (Identity provisioning,
/// Documents ID-card request, Notifications welcome message) are fully real and exercised
/// end-to-end by Student's own tests and manual verification via direct in-process calls / seed
/// data - the same "one real contract now, first real caller later" posture
/// <c>UMS.Shared.Notifications.INotificationRequestIntake</c>'s own doc comment describes for its
/// first real call sites.
/// </para>
/// </summary>
public interface IStudentRecordProvisioner
{
    /// <summary>
    /// Idempotent: a retried call carrying the same <see cref="CreateStudentRecordCommand.OriginatingApplicationId"/>
    /// returns the already-created <c>Student</c>'s result rather than erroring or duplicating
    /// (requirement-spec.md §4; edge-cases.md, "Admission's confirmation call to CreateStudentRecord
    /// is retried, invoking it twice for the same Applicant").
    /// </summary>
    public Task<Result<StudentRecordSummary>> CreateAsync(CreateStudentRecordCommand command, CancellationToken cancellationToken = default);
}

/// <summary>STU-1's internal-only creation command, exactly as a caller invokes <see cref="IStudentRecordProvisioner.CreateAsync"/>.</summary>
/// <param name="OriginatingApplicationId">Admission's own confirmed <c>Application</c> id - the natural idempotency key (design-decisions.md).</param>
/// <param name="AdmissionYear">Scopes the per-<c>(admissionYear, facultyCode)</c> <c>StudentNumber</c> sequence (design-decisions.md, "StudentNumber Generation &amp; Uniqueness Mechanism").</param>
/// <param name="FacultyCode">A short, caller-resolved code (e.g. <c>"CSE"</c>) for the <c>StudentNumber</c>'s faculty component (requirement-spec.md §9 decision 1) - the caller (Admission, once built) resolves this from Organization itself; Student does not re-derive it from <paramref name="DepartmentId"/> to avoid a second, redundant cross-module read for a value the caller already has.</param>
/// <param name="DepartmentId">Organization <c>Department</c> reference, validated for existence.</param>
/// <param name="ProgramId">Academic <c>Program</c> reference. Academic (Flow #12) does not exist yet in this build - existence is checked via a permissive first-pass stub, promoted to a real check once Academic exists (the same stub-then-promote arc <c>IOrganizationNodeExistenceChecker</c>/<c>IFacultyEmploymentChecker</c> already went through in this codebase).</param>
/// <param name="NationalId">Identity-bearing (requirement-spec.md §9 decision 2) - set once at creation, never self-service-editable afterward.</param>
public sealed record CreateStudentRecordCommand(
    Guid OriginatingApplicationId,
    int AdmissionYear,
    string FacultyCode,
    Guid DepartmentId,
    Guid ProgramId,
    string GivenName,
    string FamilyName,
    string? GivenNameBn,
    string? FamilyNameBn,
    string Email,
    string? Mobile,
    DateOnly DateOfBirth,
    string? NationalId);

/// <summary>The result of a successful (including idempotent-repeat) <see cref="IStudentRecordProvisioner.CreateAsync"/> call.</summary>
/// <param name="IdentityUserId">
/// <c>null</c> if Identity provisioning failed as a best-effort side effect of creation (see
/// <c>CreateStudentRecordService</c>'s own remarks on why Student creation itself is never rolled
/// back by a downstream integration failure) - a non-null value is the common, expected case.
/// </param>
public sealed record StudentRecordSummary(Guid StudentId, string StudentNumber, Guid? IdentityUserId, string Status);

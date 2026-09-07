using UMS.Modules.Academic.Domain.Common;
using UMS.Modules.Academic.Domain.Events;
using UMS.Shared.Domain;

namespace UMS.Modules.Academic.Domain.Enrollments;

/// <summary>
/// ACD-6/ACD-7/ACD-8: the binding of exactly one Student to one CourseOffering for one Semester -
/// the unit seat-limit and prerequisite checks apply to (docs/ddd/ubiquitous-language.md).
///
/// <para>
/// Enrollment uniqueness (requirement-spec.md §4: "Exactly one Enrollment per (Student,
/// CourseOffering, Semester)") is enforced by a DB unique index
/// (<c>ux_enrollments_student_courseoffering_semester</c>), not by this aggregate itself - a
/// duplicate insert attempt translates to <c>DuplicateValueException</c> at the DbContext layer,
/// which <c>EnrollmentService</c> treats as the idempotent "already enrolled, no-op" outcome
/// edge-cases.md's "Duplicate/double-click Enrollment submission" describes, rather than a hard
/// error.
/// </para>
/// </summary>
public sealed class Enrollment : AggregateRoot<EnrollmentId>
{
    private Enrollment()
    {
    }

    private Enrollment(
        EnrollmentId id,
        Guid studentId,
        Guid courseOfferingId,
        Guid semesterId,
        Guid sectionId,
        EnrollmentStatus status,
        CreditHours creditHoursAtEnrollment,
        string? prerequisiteOverrideReason,
        DateTimeOffset now)
    {
        Id = id;
        StudentId = studentId;
        CourseOfferingId = courseOfferingId;
        SemesterId = semesterId;
        SectionId = sectionId;
        Status = status;
        CreditHoursAtEnrollment = creditHoursAtEnrollment;
        PrerequisiteOverrideReason = prerequisiteOverrideReason;
        CreatedAt = now;
        if (status == EnrollmentStatus.Active)
        {
            ApprovedAt = now;
        }
    }

    public Guid StudentId { get; private set; }

    public Guid CourseOfferingId { get; private set; }

    public Guid SemesterId { get; private set; }

    public Guid SectionId { get; private set; }

    public EnrollmentStatus Status { get; private set; }

    public CreditHours CreditHoursAtEnrollment { get; private set; } = null!;

    /// <summary>edge-cases.md "Advisor override of a failed prerequisite check" - non-null only when an Advisor recorded an explicit, audited override; never a silent bypass.</summary>
    public string? PrerequisiteOverrideReason { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? ApprovedAt { get; private set; }

    public DateTimeOffset? DroppedAt { get; private set; }

    public string? DropReason { get; private set; }

    public Grade? Grade { get; private set; }

    /// <summary>ACD-6: raises <see cref="EnrollmentCreated"/> unconditionally (Pending or Active) - the caller (<c>EnrollmentService</c>) must only reach this from the atomic 4-check transaction's own final, all-checks-passed branch.</summary>
    public static Enrollment Create(
        Guid studentId,
        Guid courseOfferingId,
        Guid semesterId,
        Guid sectionId,
        CreditHours creditHoursAtEnrollment,
        bool requiresAdvisorApproval,
        string? prerequisiteOverrideReason,
        DateTimeOffset now)
    {
        var status = requiresAdvisorApproval ? EnrollmentStatus.Pending : EnrollmentStatus.Active;
        var enrollment = new Enrollment(EnrollmentId.New(), studentId, courseOfferingId, semesterId, sectionId, status, creditHoursAtEnrollment, prerequisiteOverrideReason, now);
        enrollment.Raise(new EnrollmentCreated(enrollment.Id.Value, studentId, courseOfferingId, semesterId, now));
        return enrollment;
    }

    /// <summary>ACD-8: the Advisor approval gate, `Pending` &#8594; `Active`.</summary>
    public void Approve(DateTimeOffset now)
    {
        if (Status != EnrollmentStatus.Pending)
        {
            throw new InvalidOperationException($"Cannot approve an Enrollment in status '{Status}' - only a Pending Enrollment can be approved.");
        }

        Status = EnrollmentStatus.Active;
        ApprovedAt = now;
    }

    /// <summary>ACD-7: the caller (<c>EnrollmentService</c>) is responsible for having already verified the Semester's drop window is open BEFORE calling this, and for releasing the CourseOffering's seat via the same atomic conditional-update mechanism ACD-6 uses, in the SAME transaction as this status change.</summary>
    public void Drop(string? reason, DateTimeOffset now)
    {
        if (Status != EnrollmentStatus.Active)
        {
            throw new InvalidOperationException($"Cannot drop an Enrollment in status '{Status}' - only an Active Enrollment can be dropped.");
        }

        Status = EnrollmentStatus.Dropped;
        DroppedAt = now;
        DropReason = reason;
        Raise(new EnrollmentDropped(Id.Value, StudentId, CourseOfferingId, reason, now));
    }

    /// <summary>ACD-10: gets (or lazily creates) this Enrollment's own single Grade child - requirement-spec.md §9 decision 2.</summary>
    public Grade EnsureGrade()
    {
        if (Grade is not null)
        {
            return Grade;
        }

        Grade = new Grade(GradeId.New(), Id);
        return Grade;
    }

    /// <summary>ACD-10: submits/resubmits marks for this Enrollment's own Grade and raises <see cref="GradeSubmitted"/>. The caller (<c>GradeService</c>) is responsible for having already re-validated the parent CourseOffering's ResultPublication is still `Draft`/`Calculated` via the state-guarded conditional update BEFORE calling this, in the same transaction.</summary>
    public Grade SubmitGrade(IReadOnlyCollection<(Guid AssessmentId, decimal Score)> scores, PercentageOrGpa calculatedScore, string letterGrade, Guid submittedByUserId, DateTimeOffset now)
    {
        var grade = EnsureGrade();
        grade.Submit(scores, calculatedScore, letterGrade, submittedByUserId, now);
        Raise(new GradeSubmitted(grade.Id.Value, Id.Value, CourseOfferingId, calculatedScore.Value, submittedByUserId, now));
        return grade;
    }
}

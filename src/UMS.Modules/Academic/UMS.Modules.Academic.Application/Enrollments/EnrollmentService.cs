using System.Text.Json;
using UMS.Modules.Academic.Application.Abstractions;
using UMS.Modules.Academic.Application.Common;
using UMS.Modules.Academic.Domain.AcademicSessions;
using UMS.Modules.Academic.Domain.Common;
using UMS.Modules.Academic.Domain.CourseOfferings;
using UMS.Modules.Academic.Domain.Courses;
using UMS.Modules.Academic.Domain.Enrollments;
using UMS.Modules.Academic.Domain.Programs;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;
using UMS.Shared.Student;

namespace UMS.Modules.Academic.Application.Enrollments;

/// <summary>
/// ACD-6/ACD-7/ACD-8: Enrollment create ("the hardest ticket") / drop / advisor-approval.
///
/// <para>
/// <b>ACD-6's atomic 4-check transaction - design-decisions.md "Atomic 4-Check Enrollment
/// Transaction Boundary Design".</b> <see cref="CreateAsync"/> opens exactly ONE database
/// transaction spanning all four checks plus the Enrollment insert, ordered cheapest/most-likely-
/// to-fail-first: prerequisite &#8594; credit-limit &#8594; timetable-conflict &#8594; seat-limit LAST. The
/// seat-limit check IS the transaction's own concurrency-safety boundary (design-decisions.md
/// "Seat-Limit Concurrency Control Pattern"): <see cref="ICourseOfferingRepository.TryIncrementEnrolledCountAsync"/>
/// issues a single atomic conditional <c>UPDATE ... WHERE enrolled_count &lt; capacity</c> - a
/// zero-rows-affected result fails the WHOLE transaction (all four checks are all-or-nothing),
/// never a partial Enrollment. This is deliberately NOT a pessimistic <c>SELECT ... FOR UPDATE</c>
/// held across all four checks, and NOT an application-level Redis lock - see that design decision
/// entry for the full comparison against <c>kart-inventory-service</c>'s own different choice.
/// </para>
///
/// <para>
/// The Student-status check (requirement-spec.md §7: a Suspended Student must not register) is a
/// cross-module read via <see cref="IStudentStatusChecker"/>, performed BEFORE the transaction
/// opens - it is not one of the "four checks" the transaction boundary is about (those are all
/// Academic-local reads against Academic's own Enrollment/Grade/CourseOffering tables).
/// </para>
/// </summary>
public sealed class EnrollmentService(
    IEnrollmentRepository enrollments,
    ICourseOfferingRepository offerings,
    ICourseRepository courses,
    IAcademicSessionRepository sessions,
    IProgramRepository programs,
    IStudentStatusChecker studentStatusChecker,
    IUnitOfWork unitOfWork,
    IAuditRecorder auditRecorder,
    IClock clock)
{
    public async Task<Result<EnrollmentDto>> CreateAsync(Guid callerUserId, CreateEnrollmentRequest request, AuditContext audit, CancellationToken cancellationToken = default)
    {
        var standing = await studentStatusChecker.GetByUserIdAsync(callerUserId, cancellationToken).ConfigureAwait(false);
        if (standing is null)
        {
            return Error.Forbidden("enrollment.no_student_record", "The calling user has no Student record and cannot enroll.");
        }

        if (!string.Equals(standing.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            // edge-cases.md "A Student's status changes to Suspended mid-registration-window":
            // blocks new Enrollment creation going forward - already-Active enrollments are
            // untouched (this check has no bearing on any existing row).
            return Error.Validation("enrollment.student_not_active", $"Student '{standing.StudentId}' is not Active (status: '{standing.Status}') and cannot register for new Enrollments.");
        }

        var offering = await offerings.GetByIdAsync(new CourseOfferingId(request.CourseOfferingId), cancellationToken).ConfigureAwait(false);
        if (offering is null)
        {
            return Error.NotFound("enrollment.courseoffering_not_found", $"No CourseOffering exists with id '{request.CourseOfferingId}'.");
        }

        var section = offering.Sections.FirstOrDefault(s => s.Id.Value == request.SectionId);
        if (section is null)
        {
            return Error.Validation("enrollment.section_not_found", $"CourseOffering '{request.CourseOfferingId}' has no Section '{request.SectionId}'.");
        }

        var semester = await sessions.GetSemesterByIdAsync(new SemesterId(offering.SemesterId), cancellationToken).ConfigureAwait(false);
        if (semester is null)
        {
            return Error.Failure("enrollment.semester_missing", "The CourseOffering's Semester could not be resolved.");
        }

        var now = clock.UtcNow;
        if (!semester.IsRegistrationOpen(now))
        {
            return Error.Validation("enrollment.registration_window_closed", $"Semester '{semester.Id}' is not currently open for registration.");
        }

        var course = await courses.GetByIdAsync(new CourseId(offering.CourseId), cancellationToken).ConfigureAwait(false);
        if (course is null)
        {
            return Error.Failure("enrollment.course_missing", "The CourseOffering's Course could not be resolved.");
        }

        var program = await programs.GetByIdAsync(new ProgramId(standing.ProgramId), cancellationToken).ConfigureAwait(false);
        if (program is null)
        {
            return Error.Validation("enrollment.program_not_found", $"No Program exists with id '{standing.ProgramId}' for this Student.");
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        // --- Check 1 (cheapest): prerequisite gate ---------------------------------------------
        if (course.Prerequisites.Count > 0 && string.IsNullOrWhiteSpace(request.PrerequisiteOverrideReason))
        {
            var prerequisiteCourseIds = course.Prerequisites.Select(p => p.PrerequisiteCourseId.Value).ToList();
            var passingCourseIds = await GetPassingCourseIdsAsync(standing.StudentId, prerequisiteCourseIds, cancellationToken).ConfigureAwait(false);
            var missing = prerequisiteCourseIds.Except(passingCourseIds).ToList();
            if (missing.Count > 0)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return Error.Validation("enrollment.prerequisite_not_met", $"Student '{standing.StudentId}' has not passed prerequisite Course(s): {string.Join(", ", missing)}.");
            }
        }

        // --- Check 2: credit-limit gate (per-student; edge-cases.md's own residual note: cannot
        // race a DIFFERENT student's concurrent enrollment, only this same student's own concurrent
        // attempts, which this single transaction's own read-then-insert already covers) ----------
        var activeEnrollments = await enrollments.GetActiveByStudentAndSemesterAsync(standing.StudentId, offering.SemesterId, cancellationToken).ConfigureAwait(false);
        var currentCredits = activeEnrollments.Sum(e => e.CreditHoursAtEnrollment.Value);
        if (currentCredits + course.CreditHours.Value > program.MaxCreditsPerSemester)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Error.Validation("enrollment.credit_limit_exceeded", $"Enrolling in '{course.Code}' ({course.CreditHours.Value} credits) would exceed Program's {program.MaxCreditsPerSemester}-credit Semester limit ({currentCredits} already Active).");
        }

        // --- Check 3: timetable-conflict gate ----------------------------------------------------
        // A pre-existing Active Enrollment for the SAME CourseOffering (a duplicate/double-click
        // resubmission, edge-cases.md's own named case) is never itself a "conflict" - its Section
        // trivially overlaps itself. That case is handled by the unique-constraint-backed
        // idempotent-return path below, not by this gate.
        foreach (var existing in activeEnrollments.Where(e => e.CourseOfferingId != offering.Id.Value))
        {
            var existingOffering = await offerings.GetByIdAsync(new CourseOfferingId(existing.CourseOfferingId), cancellationToken).ConfigureAwait(false);
            var existingSection = existingOffering?.Sections.FirstOrDefault(s => s.Id.Value == existing.SectionId);
            if (existingSection is not null && existingSection.Schedule.OverlapsWith(section.Schedule))
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return Error.Validation("enrollment.timetable_conflict", $"Section '{section.Code}' conflicts with an already-Active Enrollment's Section '{existingSection.Code}'.");
            }
        }

        // --- Check 4 (last): seat-limit gate - the atomic conditional UPDATE ---------------------
        var seatClaimed = await offerings.TryIncrementEnrolledCountAsync(offering.Id, cancellationToken).ConfigureAwait(false);
        if (!seatClaimed)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Error.Conflict("enrollment.seat_no_longer_available", $"CourseOffering '{offering.Id}' has no seat available - it filled before this request's seat-claim step.");
        }

        var enrollment = Enrollment.Create(
            standing.StudentId,
            offering.Id.Value,
            offering.SemesterId,
            section.Id.Value,
            course.CreditHours,
            program.RequiresAdvisorApproval,
            string.IsNullOrWhiteSpace(request.PrerequisiteOverrideReason) ? null : request.PrerequisiteOverrideReason.Trim(),
            now);
        enrollments.Add(enrollment);

        var auditRequest = audit.ToRequest(
            "Enrollment",
            enrollment.Id.Value.ToString(),
            "create",
            null,
            JsonSerializer.Serialize(ToDto(enrollment)),
            reason: enrollment.PrerequisiteOverrideReason,
            organizationScopeId: offering.DepartmentId);

        var commitResult = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        if (commitResult.IsFailure)
        {
            if (commitResult.Error!.Code == "enrollment.duplicate_value")
            {
                // edge-cases.md "Duplicate/double-click Enrollment submission" - idempotent: a
                // retried request against an already-created Enrollment for the same (Student,
                // CourseOffering, Semester) is a no-op, returning the existing row, not an error.
                // The seat claimed moments ago above was rolled back along with everything else in
                // this failed transaction, so no seat leaks on this path.
                var existing = await enrollments.GetByStudentCourseOfferingSemesterAsync(standing.StudentId, offering.Id.Value, offering.SemesterId, cancellationToken).ConfigureAwait(false);
                if (existing is not null)
                {
                    return ToDto(existing);
                }
            }

            return commitResult.Error!;
        }

        return ToDto(enrollment);
    }

    /// <summary>ACD-7: within the drop window, releasing the seat via the identical atomic conditional-update mechanism ACD-6 uses, same transaction as the status change (design-decisions.md "Drop-Then-Reenroll Seat-Release Ordering").</summary>
    public async Task<Result<EnrollmentDto>> DropAsync(Guid callerUserId, Guid enrollmentId, DropEnrollmentRequest request, AuditContext audit, CancellationToken cancellationToken = default)
    {
        var standing = await studentStatusChecker.GetByUserIdAsync(callerUserId, cancellationToken).ConfigureAwait(false);
        if (standing is null)
        {
            return Error.Forbidden("enrollment.no_student_record", "The calling user has no Student record.");
        }

        var enrollment = await enrollments.GetByIdAsync(new EnrollmentId(enrollmentId), cancellationToken).ConfigureAwait(false);
        if (enrollment is null)
        {
            return Error.NotFound("enrollment.not_found", $"No Enrollment exists with id '{enrollmentId}'.");
        }

        if (enrollment.StudentId != standing.StudentId)
        {
            return Error.Forbidden("enrollment.not_owned", "This Enrollment does not belong to the calling Student.");
        }

        var semester = await sessions.GetSemesterByIdAsync(new SemesterId(enrollment.SemesterId), cancellationToken).ConfigureAwait(false);
        var now = clock.UtcNow;
        if (semester is null || !semester.IsDropWindowOpen(now))
        {
            return Error.Validation("enrollment.drop_window_closed", "The drop window for this Enrollment's Semester is not currently open.");
        }

        try
        {
            enrollment.Drop(request.Reason, now);
        }
        catch (InvalidOperationException ex)
        {
            return Error.Conflict("enrollment.invalid_transition", ex.Message);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var released = await offerings.TryDecrementEnrolledCountAsync(new CourseOfferingId(enrollment.CourseOfferingId), cancellationToken).ConfigureAwait(false);
        if (!released)
        {
            // Defensive only - a CourseOffering with a genuinely Active Enrollment should always
            // have enrolled_count > 0. Still fail closed rather than silently under-count.
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Error.Failure("enrollment.seat_release_failed", "Failed to release the CourseOffering's seat - enrolled_count was already at zero.");
        }

        var auditRequest = audit.ToRequest("Enrollment", enrollment.Id.Value.ToString(), "drop", null, JsonSerializer.Serialize(ToDto(enrollment)), reason: request.Reason);
        var commitResult = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        return commitResult.IsFailure ? commitResult.Error! : ToDto(enrollment);
    }

    /// <summary>ACD-8: the Advisor approval gate, `Pending` &#8594; `Active`.</summary>
    public async Task<Result<EnrollmentDto>> ApproveAsync(Guid enrollmentId, AuditContext audit, CancellationToken cancellationToken = default)
    {
        var enrollment = await enrollments.GetByIdAsync(new EnrollmentId(enrollmentId), cancellationToken).ConfigureAwait(false);
        if (enrollment is null)
        {
            return Error.NotFound("enrollment.not_found", $"No Enrollment exists with id '{enrollmentId}'.");
        }

        try
        {
            enrollment.Approve(clock.UtcNow);
        }
        catch (InvalidOperationException ex)
        {
            return Error.Conflict("enrollment.invalid_transition", ex.Message);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var auditRequest = audit.ToRequest("Enrollment", enrollment.Id.Value.ToString(), "approve", null, JsonSerializer.Serialize(ToDto(enrollment)));
        var commitResult = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        return commitResult.IsFailure ? commitResult.Error! : ToDto(enrollment);
    }

    public async Task<Result<EnrollmentDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var enrollment = await enrollments.GetByIdAsync(new EnrollmentId(id), cancellationToken).ConfigureAwait(false);
        return enrollment is null ? Error.NotFound("enrollment.not_found", $"No Enrollment exists with id '{id}'.") : ToDto(enrollment);
    }

    internal static EnrollmentDto ToDto(Enrollment enrollment) =>
        new(
            enrollment.Id.Value,
            enrollment.StudentId,
            enrollment.CourseOfferingId,
            enrollment.SemesterId,
            enrollment.SectionId,
            enrollment.Status.ToString(),
            enrollment.CreditHoursAtEnrollment.Value,
            enrollment.PrerequisiteOverrideReason,
            enrollment.CreatedAt,
            enrollment.ApprovedAt,
            enrollment.DroppedAt);

    private async Task<HashSet<Guid>> GetPassingCourseIdsAsync(Guid studentId, IReadOnlyCollection<Guid> prerequisiteCourseIds, CancellationToken cancellationToken)
    {
        var candidates = await enrollments.GetCompletedByStudentForCoursesAsync(studentId, prerequisiteCourseIds, cancellationToken).ConfigureAwait(false);
        var passing = new HashSet<Guid>();
        foreach (var candidate in candidates)
        {
            if (candidate.Grade is { IsPassing: true })
            {
                var offering = await offerings.GetByIdAsync(new CourseOfferingId(candidate.CourseOfferingId), cancellationToken).ConfigureAwait(false);
                if (offering is not null && prerequisiteCourseIds.Contains(offering.CourseId))
                {
                    passing.Add(offering.CourseId);
                }
            }
        }

        return passing;
    }
}

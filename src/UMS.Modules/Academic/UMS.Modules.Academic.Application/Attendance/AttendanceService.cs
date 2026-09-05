using System.Text.Json;
using UMS.Modules.Academic.Application.Abstractions;
using UMS.Modules.Academic.Application.Common;
using UMS.Modules.Academic.Domain.Attendance;
using UMS.Modules.Academic.Domain.CourseOfferings;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;
using UMS.Shared.Faculty;

namespace UMS.Modules.Academic.Application.Attendance;

/// <summary>
/// ACD-9: per-session attendance recording against (CourseOffering, Enrollment)
/// (requirement-spec.md §2 Teaching &amp; Attendance).
///
/// <para>
/// <b>The permission/eligibility check below resolves instructor identity via a FRESH, synchronous
/// call to <see cref="IFacultyMemberLookup"/> on every single call</b> - never a cached/projected
/// value (design-decisions.md "In-Process Event Delivery Guarantee for InstructorAssigned
/// Projections"; edge-cases.md "InstructorAssigned event ordering race"). Only the registration-
/// browsing display (ACD-4, <c>CourseOfferingQueryService</c>) may read Academic's own already-
/// authoritative <c>CourseOffering.InstructorFacultyMemberId</c> field directly - this gate instead
/// re-verifies the FacultyMember's OWN current, authoritative employment status via Faculty's
/// interface, since that status (unlike the assignment reference itself, which Academic already
/// owns outright) can change independently of any CourseOffering-side event.
/// </para>
/// </summary>
public sealed class AttendanceService(
    IAttendanceSessionRepository attendanceSessions,
    ICourseOfferingRepository offerings,
    IFacultyMemberLookup facultyMemberLookup,
    IUnitOfWork unitOfWork,
    IAuditRecorder auditRecorder,
    IClock clock)
{
    private static readonly TimeSpan DefaultCorrectionWindow = TimeSpan.FromHours(48);

    public async Task<Result<AttendanceSessionDto>> MarkAsync(Guid callerUserId, MarkAttendanceRequest request, AuditContext audit, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<AttendanceStatus>(request.Status, ignoreCase: true, out var status))
        {
            return Error.Validation("attendance.invalid_status", $"'{request.Status}' is not a valid attendance status.");
        }

        var offering = await offerings.GetByIdAsync(new CourseOfferingId(request.CourseOfferingId), cancellationToken).ConfigureAwait(false);
        if (offering is null)
        {
            return Error.NotFound("attendance.courseoffering_not_found", $"No CourseOffering exists with id '{request.CourseOfferingId}'.");
        }

        // Fresh, synchronous Faculty lookup - see class remarks. Never trust a cached instructor
        // identity or FacultyMember status for this gate.
        var facultyMember = await facultyMemberLookup.GetByUserIdAsync(callerUserId, cancellationToken).ConfigureAwait(false);
        if (facultyMember is null)
        {
            return Error.Forbidden("attendance.no_faculty_record", "The calling user has no FacultyMember record.");
        }

        if (offering.InstructorFacultyMemberId != facultyMember.Id)
        {
            return Error.Forbidden("attendance.not_assigned_instructor", $"FacultyMember '{facultyMember.Id}' is not the assigned Instructor for CourseOffering '{offering.Id}'.");
        }

        if (!string.Equals(facultyMember.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            return Error.Forbidden("attendance.instructor_not_active", $"FacultyMember '{facultyMember.Id}' is not Active (status: '{facultyMember.Status}') and cannot record attendance.");
        }

        var now = clock.UtcNow;
        var session = await attendanceSessions.GetByCourseOfferingAndDateAsync(request.CourseOfferingId, request.SessionDate, cancellationToken).ConfigureAwait(false);
        if (session is null)
        {
            var correctionWindowClose = request.CorrectionWindowClose ?? now.Add(DefaultCorrectionWindow);
            session = AttendanceSession.Create(request.CourseOfferingId, request.SessionDate, correctionWindowClose, now);
            attendanceSessions.Add(session);
        }

        try
        {
            session.MarkOrUpdate(request.EnrollmentId, status, facultyMember.Id, now);
        }
        catch (InvalidOperationException ex)
        {
            // edge-cases.md "Attendance correction-window-close racing a late marking attempt" -
            // rejected explicitly, never silently accepted or dropped.
            return Error.Conflict("attendance.correction_window_closed", ex.Message);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var auditRequest = audit.ToRequest(
            "AttendanceSession",
            session.Id.Value.ToString(),
            "mark",
            null,
            JsonSerializer.Serialize(new { request.EnrollmentId, status = status.ToString() }),
            organizationScopeId: offering.DepartmentId);
        var commitResult = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        return commitResult.IsFailure ? commitResult.Error! : ToDto(session);
    }

    internal static AttendanceSessionDto ToDto(AttendanceSession session) =>
        new(
            session.Id.Value,
            session.CourseOfferingId,
            session.SessionDate,
            session.CorrectionWindowClose,
            session.Records.Select(r => new AttendanceRecordDto(r.EnrollmentId, r.Status.ToString(), r.MarkedByFacultyMemberId, r.MarkedAt)).ToList());
}

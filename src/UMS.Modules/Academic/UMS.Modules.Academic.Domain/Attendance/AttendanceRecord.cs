namespace UMS.Modules.Academic.Domain.Attendance;

/// <summary>
/// docs/ddd/ubiquitous-language.md: "One Student's present/absent/late/excused mark within an
/// AttendanceSession, against (CourseOffering, Enrollment)". Module-local, pending glossary merge
/// (requirement-spec.md §1/§9 decision 1) - owned by Academic even though marked by a
/// Faculty-owned identity, per module-boundaries.md's "Resolved Edge Case: Attendance".
/// </summary>
public sealed class AttendanceRecord
{
    internal AttendanceRecord(AttendanceSessionId sessionId, Guid enrollmentId, AttendanceStatus status, Guid markedByFacultyMemberId, DateTimeOffset markedAt)
    {
        Id = Guid.NewGuid();
        AttendanceSessionId = sessionId;
        EnrollmentId = enrollmentId;
        Status = status;
        MarkedByFacultyMemberId = markedByFacultyMemberId;
        MarkedAt = markedAt;
    }

    private AttendanceRecord()
    {
    }

    public Guid Id { get; private set; }

    public AttendanceSessionId AttendanceSessionId { get; private set; }

    public Guid EnrollmentId { get; private set; }

    public AttendanceStatus Status { get; private set; }

    public Guid MarkedByFacultyMemberId { get; private set; }

    public DateTimeOffset MarkedAt { get; private set; }

    /// <summary>edge-cases.md "Attendance correction-window-close racing a late marking attempt" - the caller (<c>AttendanceService</c>) must have already verified <c>now &lt;= session.CorrectionWindowClose</c> before calling this; this method itself re-asserts it as a defense-in-depth invariant, never trusting the caller alone.</summary>
    public void Update(AttendanceStatus status, Guid markedByFacultyMemberId, DateTimeOffset now, DateTimeOffset correctionWindowClose)
    {
        if (now > correctionWindowClose)
        {
            throw new InvalidOperationException("Cannot mark/edit attendance - the correction window for this session has closed.");
        }

        Status = status;
        MarkedByFacultyMemberId = markedByFacultyMemberId;
        MarkedAt = now;
    }
}

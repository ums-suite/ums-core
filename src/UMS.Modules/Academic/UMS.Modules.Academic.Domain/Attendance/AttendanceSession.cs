using UMS.Modules.Academic.Domain.Common;

namespace UMS.Modules.Academic.Domain.Attendance;

/// <summary>
/// ACD-9: one instance of attendance-taking for a CourseOffering on a given date/period
/// (docs/ddd/ubiquitous-language.md, `AttendanceSession`). <see cref="CorrectionWindowClose"/> is
/// the first-pass, administratively-configured window edge-cases.md's "Attendance correction-
/// window-close racing a late marking attempt" decision mandates - a late mark past this instant
/// is rejected outright, never silently accepted or dropped.
/// </summary>
public sealed class AttendanceSession : AggregateRoot<AttendanceSessionId>
{
    private readonly List<AttendanceRecord> _records = [];

    private AttendanceSession()
    {
    }

    private AttendanceSession(AttendanceSessionId id, Guid courseOfferingId, DateOnly sessionDate, DateTimeOffset correctionWindowClose, DateTimeOffset now)
    {
        Id = id;
        CourseOfferingId = courseOfferingId;
        SessionDate = sessionDate;
        CorrectionWindowClose = correctionWindowClose;
        CreatedAt = now;
    }

    public Guid CourseOfferingId { get; private set; }

    public DateOnly SessionDate { get; private set; }

    public DateTimeOffset CorrectionWindowClose { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyCollection<AttendanceRecord> Records => _records.AsReadOnly();

    public static AttendanceSession Create(Guid courseOfferingId, DateOnly sessionDate, DateTimeOffset correctionWindowClose, DateTimeOffset now) =>
        new(AttendanceSessionId.New(), courseOfferingId, sessionDate, correctionWindowClose, now);

    /// <summary>ACD-9: marks or re-marks one Enrollment's attendance for this session - rejected outright (via the thrown <see cref="InvalidOperationException"/>, translated to a named conflict at the application layer) once <paramref name="now"/> is past <see cref="CorrectionWindowClose"/>.</summary>
    public AttendanceRecord MarkOrUpdate(Guid enrollmentId, AttendanceStatus status, Guid markedByFacultyMemberId, DateTimeOffset now)
    {
        if (now > CorrectionWindowClose)
        {
            throw new InvalidOperationException($"Cannot mark attendance for session '{Id}' - its correction window closed at {CorrectionWindowClose:O}.");
        }

        var existing = _records.FirstOrDefault(r => r.EnrollmentId == enrollmentId);
        if (existing is not null)
        {
            existing.Update(status, markedByFacultyMemberId, now, CorrectionWindowClose);
            return existing;
        }

        var record = new AttendanceRecord(Id, enrollmentId, status, markedByFacultyMemberId, now);
        _records.Add(record);
        return record;
    }
}

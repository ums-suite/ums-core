namespace UMS.Modules.Academic.Application.Attendance;

public sealed record AttendanceSessionDto(Guid Id, Guid CourseOfferingId, DateOnly SessionDate, DateTimeOffset CorrectionWindowClose, IReadOnlyCollection<AttendanceRecordDto> Records);

public sealed record AttendanceRecordDto(Guid EnrollmentId, string Status, Guid MarkedByFacultyMemberId, DateTimeOffset MarkedAt);

public sealed record MarkAttendanceRequest(Guid CourseOfferingId, DateOnly SessionDate, DateTimeOffset? CorrectionWindowClose, Guid EnrollmentId, string Status);

using UMS.Modules.Academic.Domain.Attendance;

namespace UMS.Modules.Academic.Application.Abstractions;

public interface IAttendanceSessionRepository
{
    public Task<AttendanceSession?> GetByIdAsync(AttendanceSessionId id, CancellationToken cancellationToken = default);

    public Task<AttendanceSession?> GetByCourseOfferingAndDateAsync(Guid courseOfferingId, DateOnly sessionDate, CancellationToken cancellationToken = default);

    public void Add(AttendanceSession session);
}

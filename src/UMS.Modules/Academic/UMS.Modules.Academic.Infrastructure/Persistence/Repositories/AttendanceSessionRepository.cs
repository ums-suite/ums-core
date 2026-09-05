using Microsoft.EntityFrameworkCore;
using UMS.Modules.Academic.Application.Abstractions;
using UMS.Modules.Academic.Domain.Attendance;

namespace UMS.Modules.Academic.Infrastructure.Persistence.Repositories;

internal sealed class AttendanceSessionRepository(AcademicDbContext context) : IAttendanceSessionRepository
{
    public Task<AttendanceSession?> GetByIdAsync(AttendanceSessionId id, CancellationToken cancellationToken = default) =>
        context.AttendanceSessions.Include(s => s.Records).FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public Task<AttendanceSession?> GetByCourseOfferingAndDateAsync(Guid courseOfferingId, DateOnly sessionDate, CancellationToken cancellationToken = default) =>
        context.AttendanceSessions.Include(s => s.Records).FirstOrDefaultAsync(s => s.CourseOfferingId == courseOfferingId && s.SessionDate == sessionDate, cancellationToken);

    public void Add(AttendanceSession session) => context.AttendanceSessions.Add(session);
}

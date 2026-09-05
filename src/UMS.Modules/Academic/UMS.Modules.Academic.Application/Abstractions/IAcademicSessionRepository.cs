using UMS.Modules.Academic.Domain.AcademicSessions;

namespace UMS.Modules.Academic.Application.Abstractions;

public interface IAcademicSessionRepository
{
    public Task<AcademicSession?> GetByIdAsync(AcademicSessionId id, CancellationToken cancellationToken = default);

    public Task<Semester?> GetSemesterByIdAsync(SemesterId id, CancellationToken cancellationToken = default);

    public void Add(AcademicSession academicSession);
}

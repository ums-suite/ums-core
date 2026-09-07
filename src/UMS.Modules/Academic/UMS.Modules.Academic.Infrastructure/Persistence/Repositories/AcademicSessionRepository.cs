using Microsoft.EntityFrameworkCore;
using UMS.Modules.Academic.Application.Abstractions;
using UMS.Modules.Academic.Domain.AcademicSessions;

namespace UMS.Modules.Academic.Infrastructure.Persistence.Repositories;

internal sealed class AcademicSessionRepository(AcademicDbContext context) : IAcademicSessionRepository
{
    public Task<AcademicSession?> GetByIdAsync(AcademicSessionId id, CancellationToken cancellationToken = default) =>
        context.AcademicSessions.Include(s => s.Semesters).FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<Semester?> GetSemesterByIdAsync(SemesterId id, CancellationToken cancellationToken = default)
    {
        var session = await context.AcademicSessions
            .Include(s => s.Semesters)
            .FirstOrDefaultAsync(s => s.Semesters.Any(sm => sm.Id == id), cancellationToken)
            .ConfigureAwait(false);
        return session?.Semesters.FirstOrDefault(sm => sm.Id == id);
    }

    public void Add(AcademicSession academicSession) => context.AcademicSessions.Add(academicSession);
}

using Microsoft.EntityFrameworkCore;
using UMS.Modules.Alumni.Application.Abstractions;
using UMS.Modules.Alumni.Domain.Alumni;

namespace UMS.Modules.Alumni.Infrastructure.Persistence.Repositories;

internal sealed class AlumnusRepository(AlumniDbContext context) : IAlumnusRepository
{
    public Task<Alumnus?> GetByIdAsync(AlumnusId id, CancellationToken cancellationToken = default) =>
        context.Alumni.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public Task<Alumnus?> GetByStudentIdRefAsync(Guid studentIdRef, CancellationToken cancellationToken = default) =>
        context.Alumni.FirstOrDefaultAsync(a => a.StudentIdRef == studentIdRef, cancellationToken);

    public async Task<IReadOnlyList<Alumnus>> SearchDirectoryAsync(AlumniDirectoryFilter filter, bool includePrivate, AlumnusId? alwaysIncludeAlumnusId, int skip, int take, CancellationToken cancellationToken = default)
    {
        var query = context.Alumni.AsQueryable();

        query = includePrivate
            ? query
            : alwaysIncludeAlumnusId is { } selfId
                ? query.Where(a => a.ProfileVisibility == ProfileVisibility.Public || a.Id == selfId)
                : query.Where(a => a.ProfileVisibility == ProfileVisibility.Public);

        if (filter.GraduationYear is { } year)
        {
            query = query.Where(a => a.GraduationYear == year);
        }

        if (filter.ProgramId is { } programId)
        {
            query = query.Where(a => a.ProgramId == programId);
        }

        if (filter.DepartmentId is { } departmentId)
        {
            query = query.Where(a => a.DepartmentId == departmentId);
        }

        if (!string.IsNullOrWhiteSpace(filter.Employer))
        {
            query = query.Where(a => a.CurrentEmployer != null && EF.Functions.ILike(a.CurrentEmployer, $"%{filter.Employer}%"));
        }

        if (!string.IsNullOrWhiteSpace(filter.Location))
        {
            query = query.Where(a => a.Location != null && EF.Functions.ILike(a.Location, $"%{filter.Location}%"));
        }

        if (filter.ChapterId is { } chapterId)
        {
            var typedChapterId = new Domain.Chapters.AlumniChapterId(chapterId);
            query = query.Where(a => context.Chapters.Any(c => c.Id == typedChapterId && c.Memberships.Any(m => m.AlumnusId == a.Id)));
        }

        return await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public void Add(Alumnus alumnus) => context.Alumni.Add(alumnus);
}

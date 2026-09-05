using Microsoft.EntityFrameworkCore;
using UMS.Modules.Academic.Application.Abstractions;
using UMS.Modules.Academic.Domain.Courses;

namespace UMS.Modules.Academic.Infrastructure.Persistence.Repositories;

internal sealed class CourseRepository(AcademicDbContext context) : ICourseRepository
{
    public Task<Course?> GetByIdAsync(CourseId id, CancellationToken cancellationToken = default) =>
        context.Courses.Include(c => c.Prerequisites).FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Course>> GetByIdsAsync(IReadOnlyCollection<CourseId> ids, CancellationToken cancellationToken = default) =>
        await context.Courses.Where(c => ids.Contains(c.Id)).ToListAsync(cancellationToken).ConfigureAwait(false);

    public void Add(Course course) => context.Courses.Add(course);
}

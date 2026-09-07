using UMS.Modules.Academic.Domain.Courses;

namespace UMS.Modules.Academic.Application.Abstractions;

public interface ICourseRepository
{
    public Task<Course?> GetByIdAsync(CourseId id, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<Course>> GetByIdsAsync(IReadOnlyCollection<CourseId> ids, CancellationToken cancellationToken = default);

    public void Add(Course course);
}

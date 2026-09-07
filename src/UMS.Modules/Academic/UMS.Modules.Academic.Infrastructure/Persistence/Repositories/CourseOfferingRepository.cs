using Microsoft.EntityFrameworkCore;
using UMS.Modules.Academic.Application.Abstractions;
using UMS.Modules.Academic.Domain.CourseOfferings;

namespace UMS.Modules.Academic.Infrastructure.Persistence.Repositories;

internal sealed class CourseOfferingRepository(AcademicDbContext context) : ICourseOfferingRepository
{
    public Task<CourseOffering?> GetByIdAsync(CourseOfferingId id, CancellationToken cancellationToken = default) =>
        context.CourseOfferings.Include(o => o.Sections).Include(o => o.Exams).ThenInclude(e => e.Assessments).FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

    public async Task<IReadOnlyList<CourseOffering>> GetBySemesterAsync(Guid semesterId, CancellationToken cancellationToken = default) =>
        await context.CourseOfferings
            .Include(o => o.Sections)
            .Include(o => o.Exams).ThenInclude(e => e.Assessments)
            .Where(o => o.SemesterId == semesterId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public void Add(CourseOffering courseOffering) => context.CourseOfferings.Add(courseOffering);

    /// <summary>
    /// design-decisions.md "Seat-Limit Concurrency Control Pattern": one atomic statement -
    /// <c>UPDATE course_offerings SET enrolled_count = enrolled_count + 1 WHERE id = @id AND
    /// enrolled_count &lt; capacity</c> - via <see cref="EntityFrameworkQueryableExtensions.ExecuteUpdateAsync{TSource}"/>,
    /// which bypasses the change tracker entirely and lets Postgres itself evaluate the predicate
    /// and apply the increment as one indivisible operation. Two concurrent callers targeting the
    /// same row serialize at the database's own row-lock level; whichever commits second
    /// re-evaluates the WHERE clause against the now-committed row and, if the seat is gone,
    /// affects zero rows - exactly the race-proof guarantee this ticket exists to deliver.
    /// </summary>
    public async Task<bool> TryIncrementEnrolledCountAsync(CourseOfferingId id, CancellationToken cancellationToken = default)
    {
        var affected = await context.CourseOfferings
            .Where(o => o.Id == id && o.EnrolledCount < o.Capacity)
            .ExecuteUpdateAsync(setters => setters.SetProperty(o => o.EnrolledCount, o => o.EnrolledCount + 1), cancellationToken)
            .ConfigureAwait(false);
        return affected == 1;
    }

    /// <summary>design-decisions.md "Drop-Then-Reenroll Seat-Release Ordering": the identical atomic conditional-update shape, decrementing, guarded by <c>enrolled_count &gt; 0</c> so a defensive double-drop can never push the counter negative.</summary>
    public async Task<bool> TryDecrementEnrolledCountAsync(CourseOfferingId id, CancellationToken cancellationToken = default)
    {
        var affected = await context.CourseOfferings
            .Where(o => o.Id == id && o.EnrolledCount > 0)
            .ExecuteUpdateAsync(setters => setters.SetProperty(o => o.EnrolledCount, o => o.EnrolledCount - 1), cancellationToken)
            .ConfigureAwait(false);
        return affected == 1;
    }
}

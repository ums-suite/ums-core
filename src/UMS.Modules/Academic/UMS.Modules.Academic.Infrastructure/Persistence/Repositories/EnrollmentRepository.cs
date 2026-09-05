using Microsoft.EntityFrameworkCore;
using UMS.Modules.Academic.Application.Abstractions;
using UMS.Modules.Academic.Domain.Enrollments;

namespace UMS.Modules.Academic.Infrastructure.Persistence.Repositories;

internal sealed class EnrollmentRepository(AcademicDbContext context) : IEnrollmentRepository
{
    public Task<Enrollment?> GetByIdAsync(EnrollmentId id, CancellationToken cancellationToken = default) =>
        context.Enrollments.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public Task<Enrollment?> GetByGradeIdAsync(GradeId gradeId, CancellationToken cancellationToken = default) =>
        context.Enrollments.FirstOrDefaultAsync(e => e.Grade != null && e.Grade.Id == gradeId, cancellationToken);

    public async Task<IReadOnlyList<Enrollment>> GetActiveByStudentAndSemesterAsync(Guid studentId, Guid semesterId, CancellationToken cancellationToken = default) =>
        await context.Enrollments
            .Where(e => e.StudentId == studentId && e.SemesterId == semesterId && e.Status == EnrollmentStatus.Active)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<Enrollment>> GetByCourseOfferingAsync(Guid courseOfferingId, CancellationToken cancellationToken = default) =>
        await context.Enrollments.Where(e => e.CourseOfferingId == courseOfferingId).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<Enrollment>> GetCompletedByStudentForCoursesAsync(Guid studentId, IReadOnlyCollection<Guid> courseIds, CancellationToken cancellationToken = default)
    {
        // Materializes matching CourseOfferings first and extracts their ids client-side (rather
        // than projecting/reconstructing the CourseOfferingId value object inside a second LINQ
        // query) - constructing a value-converted struct inline inside a query predicate is not
        // reliably translatable by EF Core's Postgres provider and throws at query-execution time
        // instead of translating to SQL.
        var matchingOfferings = await context.CourseOfferings
            .Where(o => courseIds.Contains(o.CourseId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var matchingOfferingIds = matchingOfferings.Select(o => o.Id.Value).ToList();

        return await context.Enrollments
            .Where(e => e.StudentId == studentId && e.Grade != null && matchingOfferingIds.Contains(e.CourseOfferingId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Enrollment>> GetPublishedByStudentAsync(Guid studentId, CancellationToken cancellationToken = default) =>
        await context.Enrollments
            .Where(e => e.StudentId == studentId && e.Grade != null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<Enrollment?> GetByStudentCourseOfferingSemesterAsync(Guid studentId, Guid courseOfferingId, Guid semesterId, CancellationToken cancellationToken = default) =>
        context.Enrollments.FirstOrDefaultAsync(e => e.StudentId == studentId && e.CourseOfferingId == courseOfferingId && e.SemesterId == semesterId, cancellationToken);

    public void Add(Enrollment enrollment) => context.Enrollments.Add(enrollment);
}

using Microsoft.EntityFrameworkCore;
using UMS.Modules.Faculty.Application.Abstractions;
using UMS.Modules.Faculty.Domain.CourseAssignments;

namespace UMS.Modules.Faculty.Infrastructure.Persistence.Repositories;

internal sealed class CourseAssignmentRepository(FacultyDbContext context) : ICourseAssignmentRepository
{
    public Task<CourseAssignment?> GetForUpdateAsync(Guid facultyMemberId, Guid courseOfferingId, CancellationToken cancellationToken = default) =>
        context.CourseAssignments
            .FromSqlInterpolated($"SELECT *, xmin FROM faculty.course_assignments WHERE faculty_member_id = {facultyMemberId} AND course_offering_id = {courseOfferingId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<CourseAssignment>> ListByFacultyMemberAsync(Guid facultyMemberId, CancellationToken cancellationToken = default) =>
        await context.CourseAssignments
            .Where(c => c.FacultyMemberId == facultyMemberId)
            .OrderByDescending(c => c.AssignedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public void Add(CourseAssignment courseAssignment) => context.CourseAssignments.Add(courseAssignment);
}

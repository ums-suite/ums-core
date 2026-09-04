using UMS.Modules.Faculty.Domain.CourseAssignments;

namespace UMS.Modules.Faculty.Application.Abstractions;

public interface ICourseAssignmentRepository
{
    /// <summary>Row-locked read, keyed by the natural (FacultyMemberId, CourseOfferingId) pair - the idempotent-upsert key (design-decisions.md, "Event Consumer Idempotency and Ordering").</summary>
    public Task<CourseAssignment?> GetForUpdateAsync(Guid facultyMemberId, Guid courseOfferingId, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<CourseAssignment>> ListByFacultyMemberAsync(Guid facultyMemberId, CancellationToken cancellationToken = default);

    public void Add(CourseAssignment courseAssignment);
}

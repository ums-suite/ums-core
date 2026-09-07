using UMS.Modules.Academic.Domain.Enrollments;

namespace UMS.Modules.Academic.Application.Abstractions;

public interface IEnrollmentRepository
{
    public Task<Enrollment?> GetByIdAsync(EnrollmentId id, CancellationToken cancellationToken = default);

    public Task<Enrollment?> GetByGradeIdAsync(GradeId gradeId, CancellationToken cancellationToken = default);

    /// <summary>edge-cases.md "Duplicate/double-click Enrollment submission" - the idempotent-lookup path a caught unique-constraint violation falls back to.</summary>
    public Task<Enrollment?> GetByStudentCourseOfferingSemesterAsync(Guid studentId, Guid courseOfferingId, Guid semesterId, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<Enrollment>> GetActiveByStudentAndSemesterAsync(Guid studentId, Guid semesterId, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<Enrollment>> GetByCourseOfferingAsync(Guid courseOfferingId, CancellationToken cancellationToken = default);

    /// <summary>ACD-6's prerequisite gate: every one of the Student's own Enrollments across every Semester whose Grade counts as "passing" for <paramref name="courseIds"/>' worth of prerequisite Courses.</summary>
    public Task<IReadOnlyList<Enrollment>> GetCompletedByStudentForCoursesAsync(Guid studentId, IReadOnlyCollection<Guid> courseIds, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<Enrollment>> GetPublishedByStudentAsync(Guid studentId, CancellationToken cancellationToken = default);

    public void Add(Enrollment enrollment);
}

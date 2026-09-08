using UMS.Shared.ErrorHandling.Results;
using UMS.Shared.Student;

namespace UMS.Modules.Hostel.Application.Common;

/// <summary>
/// Resolves the caller's own <c>StudentId</c> from their Identity <c>UserId</c> via
/// <c>UMS.Shared.Student.IStudentStatusChecker</c> (module-boundaries.md: Hostel depends on Student) -
/// mirrors how Admission's own <c>OwnershipGuard</c> resolves an <c>ApplicantId</c>, and how Academic's
/// <c>EnrollmentService</c> already uses this exact contract for its own eligibility gate.
/// </summary>
public sealed class StudentContextService(IStudentStatusChecker studentStatusChecker)
{
    public async Task<Result<Guid>> ResolveOwnStudentIdAsync(Guid identityUserId, CancellationToken cancellationToken = default)
    {
        var standing = await studentStatusChecker.GetByUserIdAsync(identityUserId, cancellationToken).ConfigureAwait(false);
        return standing is null
            ? Error.Forbidden("hostel.no_student_record", "The calling user has no Student record.")
            : standing.StudentId;
    }

    public Task<StudentAcademicStanding?> GetStandingAsync(Guid studentId, CancellationToken cancellationToken = default) =>
        studentStatusChecker.GetByStudentIdAsync(studentId, cancellationToken);
}

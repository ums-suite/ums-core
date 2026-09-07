using UMS.Modules.Academic.Application.Abstractions;
using UMS.Modules.Academic.Domain.CourseOfferings;
using UMS.Modules.Academic.Domain.Enrollments;
using UMS.Shared.Academic;
using UMS.Shared.Faculty;
using UMS.Shared.Student;

namespace UMS.Modules.Academic.Infrastructure.CrossModule;

/// <summary>
/// The one real implementation of <see cref="ICourseOfferingLookup"/>
/// (release/DEVELOPMENT_PLAN.md Flow #13, LRN-1) - the same in-process, shared-interface pattern
/// Faculty's own <c>FacultyMemberLookupAdapter</c> and Student's <c>StudentStatusCheckerAdapter</c>
/// already established, applied to Academic's first outward-facing read contract.
///
/// <para>
/// Both the instructor check and the enrollment lookup do the Identity-user-id resolution HERE,
/// inside Academic, precisely so the calling module (Learning) needs no Faculty or Student
/// dependency of its own - see <see cref="ICourseOfferingLookup"/>'s own remarks. The instructor
/// check re-verifies the FacultyMember's own current <c>Active</c> status on every call, matching
/// <c>AttendanceService</c>'s fresh-lookup discipline rather than trusting a cached assignment.
/// </para>
/// </summary>
internal sealed class CourseOfferingLookupAdapter(
    ICourseOfferingRepository offerings,
    IEnrollmentRepository enrollments,
    IFacultyMemberLookup facultyMemberLookup,
    IStudentStatusChecker studentStatusChecker) : ICourseOfferingLookup
{
    public async Task<CourseOfferingSummary?> GetAsync(Guid courseOfferingId, CancellationToken cancellationToken = default)
    {
        var offering = await offerings.GetByIdAsync(new CourseOfferingId(courseOfferingId), cancellationToken).ConfigureAwait(false);
        return offering is null
            ? null
            : new CourseOfferingSummary(offering.Id.Value, offering.CourseId, offering.SemesterId, offering.DepartmentId, offering.InstructorFacultyMemberId);
    }

    public async Task<bool> IsInstructorForOfferingAsync(Guid courseOfferingId, Guid identityUserId, CancellationToken cancellationToken = default)
    {
        var offering = await offerings.GetByIdAsync(new CourseOfferingId(courseOfferingId), cancellationToken).ConfigureAwait(false);
        if (offering?.InstructorFacultyMemberId is not { } assignedInstructorId)
        {
            return false;
        }

        var facultyMember = await facultyMemberLookup.GetByUserIdAsync(identityUserId, cancellationToken).ConfigureAwait(false);
        return facultyMember is not null
            && facultyMember.Id == assignedInstructorId
            && string.Equals(facultyMember.Status, "Active", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<EnrolledStudentSummary?> GetEnrolledStudentAsync(Guid courseOfferingId, Guid identityUserId, CancellationToken cancellationToken = default)
    {
        var standing = await studentStatusChecker.GetByUserIdAsync(identityUserId, cancellationToken).ConfigureAwait(false);
        if (standing is null)
        {
            return null;
        }

        var enrollment = await FindActiveEnrollmentAsync(courseOfferingId, standing.StudentId, cancellationToken).ConfigureAwait(false);
        return enrollment is null
            ? null
            : new EnrolledStudentSummary(enrollment.StudentId, enrollment.Id.Value, enrollment.Status.ToString(), standing.IdentityUserId ?? identityUserId);
    }

    public async Task<EnrolledStudentSummary?> GetEnrolledStudentByStudentIdAsync(Guid courseOfferingId, Guid studentId, CancellationToken cancellationToken = default)
    {
        var enrollment = await FindActiveEnrollmentAsync(courseOfferingId, studentId, cancellationToken).ConfigureAwait(false);
        if (enrollment is null)
        {
            return null;
        }

        var standing = await studentStatusChecker.GetByStudentIdAsync(studentId, cancellationToken).ConfigureAwait(false);
        return new EnrolledStudentSummary(enrollment.StudentId, enrollment.Id.Value, enrollment.Status.ToString(), standing?.IdentityUserId);
    }

    private async Task<Enrollment?> FindActiveEnrollmentAsync(Guid courseOfferingId, Guid studentId, CancellationToken cancellationToken)
    {
        var offeringEnrollments = await enrollments.GetByCourseOfferingAsync(courseOfferingId, cancellationToken).ConfigureAwait(false);
        return offeringEnrollments.FirstOrDefault(e => e.StudentId == studentId && e.Status != EnrollmentStatus.Dropped);
    }
}

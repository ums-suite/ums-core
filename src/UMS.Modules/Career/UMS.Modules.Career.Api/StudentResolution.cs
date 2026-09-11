using UMS.Shared.ErrorHandling.Results;
using UMS.Shared.Student;

namespace UMS.Modules.Career.Api;

/// <summary>
/// Every Career endpoint that acts on behalf of "the current Student" (ResumeProfile management,
/// booking/cancelling an InterviewSlot, withdrawing a CareerApplication, listing "my applications")
/// needs the real `Student.Id` - never the Identity `User.Id` from the caller's JWT `sub` claim
/// directly - resolved fresh via `IStudentStatusChecker.GetByUserIdAsync`, mirroring
/// `InternshipApplicationService`'s/`DriveApplicationService`'s own inline resolution exactly (this
/// helper exists only to avoid repeating that same three-line lookup across every other endpoint
/// file that needs it).
/// </summary>
internal static class StudentResolution
{
    public static async Task<Result<Guid>> ResolveStudentIdAsync(this IStudentStatusChecker checker, Guid identityUserId, CancellationToken cancellationToken)
    {
        var standing = await checker.GetByUserIdAsync(identityUserId, cancellationToken).ConfigureAwait(false);
        return standing is null
            ? Error.Failure("career.student_unresolved", "Could not resolve a Student record for the current user.")
            : standing.StudentId;
    }
}

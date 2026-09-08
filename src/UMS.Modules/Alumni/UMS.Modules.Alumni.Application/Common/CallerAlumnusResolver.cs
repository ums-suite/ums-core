using UMS.Modules.Alumni.Application.Abstractions;
using UMS.Modules.Alumni.Domain.Alumni;
using UMS.Shared.Student;

namespace UMS.Modules.Alumni.Application.Common;

/// <summary>
/// Resolves the calling Identity <c>User</c>'s own <see cref="Alumnus"/> record, for every
/// self-service endpoint that needs "is this caller an Alumnus, and which one" (own profile, chapter
/// join/leave, job posting as an alumnus poster, donation initiation, mentorship mentor opt-in, event
/// RSVP). <see cref="Alumnus"/> links immutably to <c>StudentIdRef</c>, not directly to an Identity
/// <c>User</c> id - so this goes through the SAME sanctioned indirect path the ALM-1 StudentGraduated
/// consumption itself uses (<c>UMS.Shared.Student.IStudentStatusChecker</c>), turning the caller's JWT
/// identity into a StudentId and then into the linked Alumnus, exactly as Academic's own Enrollment
/// endpoints already turn a Student caller's JWT identity into their StudentId via the same contract's
/// <c>GetByUserIdAsync</c> half.
/// </summary>
public sealed class CallerAlumnusResolver(IAlumnusRepository alumni, IStudentStatusChecker studentStatusChecker)
{
    public async Task<Alumnus?> ResolveAsync(Guid identityUserId, CancellationToken cancellationToken = default)
    {
        var standing = await studentStatusChecker.GetByUserIdAsync(identityUserId, cancellationToken).ConfigureAwait(false);
        return standing is null
            ? null
            : await alumni.GetByStudentIdRefAsync(standing.StudentId, cancellationToken).ConfigureAwait(false);
    }
}

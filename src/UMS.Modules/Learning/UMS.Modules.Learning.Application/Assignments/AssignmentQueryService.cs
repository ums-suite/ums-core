using UMS.Modules.Learning.Application.Abstractions;
using UMS.Modules.Learning.Domain.Assignments;
using UMS.Shared.Academic;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Learning.Application.Assignments;

/// <summary>
/// LRN-4: <c>GET /assignments?courseOfferingId=</c> and <c>GET /assignments/{id}</c>
/// (requirement-spec.md §6, "Authenticated (enrolled Student or Instructor)").
///
/// <para>
/// Read scoping is a real check, not a bare authentication gate: the caller must be either the
/// offering's assigned Instructor or an enrolled Student, both resolved through Academic's own
/// lookup. A <c>Draft</c> Assignment is additionally Instructor-only - a Student must never see a
/// task that has not been published to them yet.
/// </para>
/// </summary>
public sealed class AssignmentQueryService(IAssignmentRepository assignments, ICourseOfferingLookup courseOfferings)
{
    public async Task<Result<IReadOnlyList<AssignmentDto>>> ListByCourseOfferingAsync(Guid courseOfferingId, Guid callerUserId, CancellationToken cancellationToken = default)
    {
        var access = await ResolveAccessAsync(courseOfferingId, callerUserId, cancellationToken).ConfigureAwait(false);
        if (access.IsFailure)
        {
            return access.Error!;
        }

        var items = await assignments.GetByCourseOfferingAsync(courseOfferingId, cancellationToken).ConfigureAwait(false);
        var visible = access.Value.IsInstructor
            ? items
            : items.Where(a => a.Status != AssignmentStatus.Draft).ToList();

        return visible.Select(AssignmentService.ToDto).ToList();
    }

    public async Task<Result<AssignmentDto>> GetByIdAsync(Guid assignmentId, Guid callerUserId, CancellationToken cancellationToken = default)
    {
        var assignment = await assignments.GetByIdAsync(new AssignmentId(assignmentId), cancellationToken).ConfigureAwait(false);
        if (assignment is null)
        {
            return Error.NotFound("assignment.not_found", $"No Assignment exists with id '{assignmentId}'.");
        }

        var access = await ResolveAccessAsync(assignment.CourseOfferingId, callerUserId, cancellationToken).ConfigureAwait(false);
        if (access.IsFailure)
        {
            return access.Error!;
        }

        return !access.Value.IsInstructor && assignment.Status == AssignmentStatus.Draft
            ? Error.NotFound("assignment.not_found", $"No Assignment exists with id '{assignmentId}'.")
            : AssignmentService.ToDto(assignment);
    }

    /// <summary>Shared enrollment/instructor scoping used by every enrollment-scoped read in this module.</summary>
    internal static async Task<Result<CourseOfferingAccess>> ResolveAccessAsync(
        ICourseOfferingLookup courseOfferings,
        Guid courseOfferingId,
        Guid callerUserId,
        CancellationToken cancellationToken)
    {
        if (await courseOfferings.IsInstructorForOfferingAsync(courseOfferingId, callerUserId, cancellationToken).ConfigureAwait(false))
        {
            return new CourseOfferingAccess(true, null);
        }

        var enrolled = await courseOfferings.GetEnrolledStudentAsync(courseOfferingId, callerUserId, cancellationToken).ConfigureAwait(false);
        return enrolled is null
            ? Error.Forbidden("learning.not_enrolled_or_instructor", $"The calling user is neither the assigned Instructor nor an enrolled Student for CourseOffering '{courseOfferingId}'.")
            : new CourseOfferingAccess(false, enrolled.StudentId);
    }

    private Task<Result<CourseOfferingAccess>> ResolveAccessAsync(Guid courseOfferingId, Guid callerUserId, CancellationToken cancellationToken) =>
        ResolveAccessAsync(courseOfferings, courseOfferingId, callerUserId, cancellationToken);
}

/// <summary>Who the caller is relative to one <c>CourseOffering</c>. <paramref name="StudentId"/> is populated only for an enrolled Student.</summary>
public sealed record CourseOfferingAccess(bool IsInstructor, Guid? StudentId);

using UMS.Modules.Learning.Application.Abstractions;
using UMS.Modules.Learning.Domain.Assignments;
using UMS.Modules.Learning.Domain.Submissions;
using UMS.Shared.Academic;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Learning.Application.Submissions;

/// <summary>
/// LRN-10: the Instructor's grading queue (<c>GET /assignments/{id}/submissions</c>) and the
/// Student-owned-or-Instructor single read (<c>GET /submissions/{id}</c>), requirement-spec.md §6.
///
/// <para>
/// The grading queue returns the latest non-superseded attempt per Student by default - the "which
/// one counts" ambiguity edge-cases.md's superseded-chain decision exists to resolve. Prior
/// attempts stay individually addressable by their own id through
/// <see cref="GetByIdAsync"/>.
/// </para>
/// </summary>
public sealed class SubmissionQueryService(
    IAssignmentRepository assignments,
    ISubmissionRepository submissions,
    ICourseOfferingLookup courseOfferings)
{
    public async Task<Result<IReadOnlyList<SubmissionDto>>> ListForAssignmentAsync(
        Guid assignmentId,
        Guid callerUserId,
        bool includeSuperseded,
        CancellationToken cancellationToken = default)
    {
        var assignment = await assignments.GetByIdAsync(new AssignmentId(assignmentId), cancellationToken).ConfigureAwait(false);
        if (assignment is null)
        {
            return Error.NotFound("assignment.not_found", $"No Assignment exists with id '{assignmentId}'.");
        }

        var isInstructor = await courseOfferings.IsInstructorForOfferingAsync(assignment.CourseOfferingId, callerUserId, cancellationToken).ConfigureAwait(false);
        if (!isInstructor)
        {
            return Error.Forbidden("submission.not_assigned_instructor", $"The calling user is not the assigned, active Instructor for CourseOffering '{assignment.CourseOfferingId}'.");
        }

        var all = await submissions.GetByAssignmentAsync(assignment.Id, cancellationToken).ConfigureAwait(false);
        var visible = includeSuperseded ? all : all.Where(s => s.Status == SubmissionStatus.Submitted).ToList();
        return visible.Select(SubmissionService.ToDto).ToList();
    }

    /// <summary>requirement-spec.md §6: "Student-owned or Instructor (course-scoped)". A Student may read their OWN attempts, superseded ones included - never another Student's.</summary>
    public async Task<Result<SubmissionDto>> GetByIdAsync(Guid submissionId, Guid callerUserId, CancellationToken cancellationToken = default)
    {
        var submission = await submissions.GetByIdAsync(new SubmissionId(submissionId), cancellationToken).ConfigureAwait(false);
        if (submission is null)
        {
            return Error.NotFound("submission.not_found", $"No Submission exists with id '{submissionId}'.");
        }

        if (await courseOfferings.IsInstructorForOfferingAsync(submission.CourseOfferingId, callerUserId, cancellationToken).ConfigureAwait(false))
        {
            return SubmissionService.ToDto(submission);
        }

        var enrolled = await courseOfferings.GetEnrolledStudentAsync(submission.CourseOfferingId, callerUserId, cancellationToken).ConfigureAwait(false);
        return enrolled?.StudentId == submission.StudentId
            ? SubmissionService.ToDto(submission)
            : Error.Forbidden("submission.forbidden", "You may only read your own Submissions.");
    }
}

using System.Text.Json;
using UMS.Modules.Learning.Application.Abstractions;
using UMS.Modules.Learning.Application.Common;
using UMS.Modules.Learning.Domain.Assignments;
using UMS.Modules.Learning.Domain.Submissions;
using UMS.Shared.Academic;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Learning.Application.Submissions;

/// <summary>
/// LRN-6/LRN-7/LRN-11: submission intake, the superseded-chain resubmission path, and Instructor
/// evaluation.
///
/// <para>
/// <b>The accept check takes no lock and touches no shared counter.</b> design-decisions.md's "Why
/// Submission Timing Doesn't Need Seat-Limit-Style Concurrency Control": each Student's Submission
/// is an independent row and the accept/reject decision depends only on that one request's own
/// server-received timestamp compared against static, already-computed values. There is
/// deliberately no <c>SELECT ... FOR UPDATE</c>, no <c>ExecuteUpdateAsync</c> conditional claim, and
/// no counter anywhere in this method - reusing Academic's seat-limit mechanism here would be
/// applying a fix to a problem that structurally does not exist.
/// </para>
///
/// <para>
/// <b><c>submittedAt</c> comes from <see cref="IClock"/>, never from the request.</b>
/// <see cref="CreateSubmissionRequest"/> has no timestamp field at all - requirement-spec.md §4's
/// server-authoritative invariant enforced by shape rather than by validation.
/// </para>
/// </summary>
public sealed class SubmissionService(
    IAssignmentRepository assignments,
    ISubmissionRepository submissions,
    ICourseOfferingLookup courseOfferings,
    IUnitOfWork unitOfWork,
    IAuditRecorder auditRecorder,
    IClock clock)
{
    public async Task<Result<SubmissionDto>> SubmitAsync(Guid assignmentId, Guid callerUserId, CreateSubmissionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var assignment = await assignments.GetByIdAsync(new AssignmentId(assignmentId), cancellationToken).ConfigureAwait(false);
        if (assignment is null)
        {
            return Error.NotFound("assignment.not_found", $"No Assignment exists with id '{assignmentId}'.");
        }

        var enrolled = await courseOfferings.GetEnrolledStudentAsync(assignment.CourseOfferingId, callerUserId, cancellationToken).ConfigureAwait(false);
        if (enrolled is null)
        {
            return Error.Forbidden("submission.not_enrolled", $"The calling user is not an enrolled Student in CourseOffering '{assignment.CourseOfferingId}'.");
        }

        var files = new List<SubmissionFile>();
        foreach (var file in request.Files ?? [])
        {
            var created = SubmissionFile.Create(file.ArtifactId, file.FileName, file.MimeType);
            if (created.IsFailure)
            {
                return created.Error!;
            }

            files.Add(created.Value);
        }

        var previous = await submissions.GetCountedForStudentAsync(assignment.Id, enrolled.StudentId, cancellationToken).ConfigureAwait(false);
        if (previous is not null && !assignment.AllowResubmission)
        {
            // edge-cases.md "A Student submits twice" residual note: an Assignment with
            // resubmission disabled rejects a second attempt OUTRIGHT rather than creating a new,
            // superseding one.
            return Error.Conflict("submission.resubmission_not_allowed", $"Assignment '{assignmentId}' accepts a single attempt only and you have already submitted.");
        }

        // The whole accept/reject decision - a stateless comparison against the SERVER's clock.
        var submittedAt = clock.UtcNow;
        var acceptance = assignment.EvaluateAcceptance(submittedAt, enrolled.StudentId);

        var submission = Submission.Create(assignment, enrolled.StudentId, callerUserId, request.TextContent, files, acceptance, submittedAt);
        if (submission.IsFailure)
        {
            return submission.Error!;
        }

        // LRN-7: the supersession happens in the SAME transaction as the new row's insert, so
        // there is never an instant at which two non-superseded attempts both look "counted".
        if (previous is not null)
        {
            var superseded = previous.Supersede(submission.Value.Id, submittedAt);
            if (superseded.IsFailure)
            {
                return superseded.Error!;
            }
        }

        submissions.Add(submission.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(submission.Value);
    }

    /// <summary>
    /// LRN-11: records an <c>AssignmentScore</c> and publishes <c>SubmissionEvaluated</c>, written
    /// to Audit synchronously in the same transaction (requirement-spec.md §5 Auditability).
    ///
    /// <para>
    /// <b>Never consults the Submission's PlagiarismCheck.</b> requirement-spec.md §4: a Submission
    /// can be evaluated regardless of check status, <c>Failed</c> included - a third-party vendor's
    /// availability must never dictate whether Learning's grading pipeline can proceed.
    /// </para>
    /// </summary>
    public async Task<Result<SubmissionDto>> EvaluateAsync(
        Guid submissionId,
        Guid callerUserId,
        EvaluateSubmissionRequest request,
        AuditContext audit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(audit);

        var submission = await submissions.GetByIdAsync(new SubmissionId(submissionId), cancellationToken).ConfigureAwait(false);
        if (submission is null)
        {
            return Error.NotFound("submission.not_found", $"No Submission exists with id '{submissionId}'.");
        }

        var isInstructor = await courseOfferings.IsInstructorForOfferingAsync(submission.CourseOfferingId, callerUserId, cancellationToken).ConfigureAwait(false);
        if (!isInstructor)
        {
            return Error.Forbidden("submission.not_assigned_instructor", $"The calling user is not the assigned, active Instructor for CourseOffering '{submission.CourseOfferingId}'.");
        }

        var assignment = await assignments.GetByIdAsync(submission.AssignmentId, cancellationToken).ConfigureAwait(false);
        if (assignment is null)
        {
            return Error.NotFound("assignment.not_found", $"No Assignment exists with id '{submission.AssignmentId}'.");
        }

        var evaluated = submission.Evaluate(request.Points, assignment.MaxPoints, request.Feedback, callerUserId, clock.UtcNow);
        if (evaluated.IsFailure)
        {
            return evaluated.Error!;
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var auditRequest = audit.ToRequest(
            "Submission",
            submissionId.ToString(),
            "evaluate",
            null,
            JsonSerializer.Serialize(new
            {
                rawPoints = submission.Score!.RawPoints,
                awardedPoints = submission.Score.AwardedPoints,
                maxPoints = submission.Score.MaxPoints,
                appliedLatePenaltyPercentage = submission.Score.AppliedLatePenaltyPercentage,
            }));

        var commit = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        return commit.IsFailure ? commit.Error! : ToDto(submission);
    }

    internal static SubmissionDto ToDto(Submission submission)
    {
        var check = submission.LatestPlagiarismCheck;

        AssignmentScoreDto? score = submission.Score is null
            ? null
            : new AssignmentScoreDto(
                submission.Score.RawPoints,
                submission.Score.AwardedPoints,
                submission.Score.MaxPoints,
                submission.Score.AppliedLatePenaltyPercentage,
                submission.Score.Feedback);

        PlagiarismCheckDto? checkDto = check is null
            ? null
            : new PlagiarismCheckDto(
                check.Id.Value,
                check.Status.ToString(),
                check.Score?.SimilarityPercentage,
                check.Score?.MatchedSourceSummary,
                check.Score?.ProviderName,
                check.AttemptCount,
                check.QueuedAt,
                check.CompletedAt,
                check.FailureReason);

        return new SubmissionDto(
            submission.Id.Value,
            submission.AssignmentId.Value,
            submission.CourseOfferingId,
            submission.StudentId,
            submission.TextContent,
            submission.Files.Select(f => new SubmissionFileDto(f.ArtifactId, f.FileName, f.MimeType)).ToList(),
            submission.SubmittedAt,
            submission.IsLate,
            submission.LatePenaltyPercentage,
            submission.Status.ToString(),
            submission.SupersededBySubmissionId?.Value,
            submission.SupersededAt,
            score,
            submission.EvaluatedByUserId,
            submission.EvaluatedAt,
            checkDto);
    }
}

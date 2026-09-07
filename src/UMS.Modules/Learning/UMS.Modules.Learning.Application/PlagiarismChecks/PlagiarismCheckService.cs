using UMS.Modules.Learning.Application.Abstractions;
using UMS.Modules.Learning.Application.Submissions;
using UMS.Modules.Learning.Domain.PlagiarismChecks;
using UMS.Modules.Learning.Domain.Submissions;
using UMS.Shared.Academic;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Learning.Application.PlagiarismChecks;

/// <summary>
/// LRN-8/LRN-9: the whole PlagiarismCheck lifecycle, exactly as design-decisions.md's
/// "PlagiarismCheck Execution Timing &amp; Resilience" specifies it.
///
/// <list type="bullet">
/// <item><description><see cref="EnqueueForSubmissionAsync"/> - triggered on <c>SubmissionCreated</c> by this module's own outbox relay worker, so a Student's submit request never waits on a third-party provider.</description></item>
/// <item><description><see cref="RunQueuedAsync"/> - drives one queued check through the Polly-wrapped provider call; a superseded Submission's still-running check was already cancelled by <c>Submission.Supersede</c>, so no provider quota is spent on moot content.</description></item>
/// <item><description><see cref="RequeueUncheckedForAssignmentAsync"/> - the <c>hardCloseAt</c> safety net: every counted Submission with no Completed check gets one more attempt at window close.</description></item>
/// <item><description><see cref="RetryAsync"/> - the Instructor's manual re-trigger after a <c>Failed</c> check, available immediately rather than waiting for the sweep.</description></item>
/// </list>
///
/// <para>
/// A provider failure produces a real, terminal <c>Failed</c> status - never a fabricated clean
/// result, and never an indefinite "still checking". Nothing here ever blocks
/// <c>Submission.Evaluate()</c>.
/// </para>
/// </summary>
public sealed class PlagiarismCheckService(
    IAssignmentRepository assignments,
    ISubmissionRepository submissions,
    ICourseOfferingLookup courseOfferings,
    IPlagiarismCheckProvider provider,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    /// <summary>Called by the relay worker for a <c>SubmissionCreated</c> outbox message. Idempotent: a repeated pass against a Submission that already has a live or completed check is a no-op, not a second provider call.</summary>
    public async Task<Result> EnqueueForSubmissionAsync(Guid submissionId, CancellationToken cancellationToken = default)
    {
        var submission = await submissions.GetByIdAsync(new SubmissionId(submissionId), cancellationToken).ConfigureAwait(false);
        if (submission is null)
        {
            return Result.Failure(Error.NotFound("submission.not_found", $"No Submission exists with id '{submissionId}'."));
        }

        if (submission.Status == SubmissionStatus.Superseded)
        {
            // Already made moot by a resubmission between the event being written and this pass.
            // Not an error - just nothing to do.
            return Result.Success();
        }

        var enqueued = submission.EnqueuePlagiarismCheck(clock.UtcNow);
        if (enqueued.IsFailure)
        {
            return Result.Failure(enqueued.Error!);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    /// <summary>Claims and drives up to <paramref name="batchSize"/> queued checks. Returns how many were driven to a terminal outcome.</summary>
    public async Task<int> RunQueuedAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        var pending = await submissions.GetWithQueuedPlagiarismChecksAsync(batchSize, cancellationToken).ConfigureAwait(false);
        var processed = 0;

        foreach (var submission in pending)
        {
            if (await RunOneAsync(submission, cancellationToken).ConfigureAwait(false))
            {
                processed++;
            }
        }

        return processed;
    }

    /// <summary>LRN-9's window-close safety net (edge-cases.md's own residual note: "automatically re-attempted once when the Assignment's window reaches hardCloseAt").</summary>
    public async Task<int> RequeueUncheckedForAssignmentAsync(Guid assignmentId, CancellationToken cancellationToken = default)
    {
        var candidates = await submissions.GetCountedWithoutCompletedCheckAsync(new Domain.Assignments.AssignmentId(assignmentId), cancellationToken).ConfigureAwait(false);
        var requeued = 0;

        foreach (var submission in candidates)
        {
            var result = submission.RetryPlagiarismCheck(clock.UtcNow);
            if (result.IsSuccess)
            {
                requeued++;
            }
        }

        if (requeued > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return requeued;
    }

    /// <summary>requirement-spec.md §6 <c>POST /submissions/{id}/plagiarism-check/retry</c> - the Instructor's manual re-trigger, gated on Instructor ownership of the offering.</summary>
    public async Task<Result<PlagiarismCheckDto>> RetryAsync(Guid submissionId, Guid callerUserId, CancellationToken cancellationToken = default)
    {
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

        var retried = submission.RetryPlagiarismCheck(clock.UtcNow);
        if (retried.IsFailure)
        {
            return retried.Error!;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(retried.Value);
    }

    /// <summary>requirement-spec.md §6 <c>GET /submissions/{id}/plagiarism-check</c>. Never gates evaluation - this is a read, surfaced alongside the Submission an Instructor is about to grade.</summary>
    public async Task<Result<PlagiarismCheckDto>> GetForSubmissionAsync(Guid submissionId, Guid callerUserId, CancellationToken cancellationToken = default)
    {
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

        var check = submission.LatestPlagiarismCheck;
        return check is null
            ? Error.NotFound("plagiarism_check.not_found", $"No PlagiarismCheck has been enqueued for Submission '{submissionId}' yet.")
            : ToDto(check);
    }

    internal static PlagiarismCheckDto ToDto(PlagiarismCheck check) => new(
        check.Id.Value,
        check.Status.ToString(),
        check.Score?.SimilarityPercentage,
        check.Score?.MatchedSourceSummary,
        check.Score?.ProviderName,
        check.AttemptCount,
        check.QueuedAt,
        check.CompletedAt,
        check.FailureReason);

    private async Task<bool> RunOneAsync(Submission submission, CancellationToken cancellationToken)
    {
        if (submission.LatestPlagiarismCheck is not { Status: PlagiarismCheckStatus.Queued } check)
        {
            return false;
        }

        if (submission.Status == SubmissionStatus.Superseded)
        {
            // design-decisions.md: cancelled rather than run - provider quota is metered, and this
            // content is already moot.
            check.Cancel(clock.UtcNow);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return false;
        }

        var started = check.Start(clock.UtcNow);
        if (started.IsFailure)
        {
            return false;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // The one external call in this module. UMS.Shared.Resilience's standard Polly pipeline
        // (retry with backoff+jitter, circuit breaker, timeout) sits between here and the provider -
        // reaching a failure result below means that whole pipeline already gave up.
        var providerResult = await provider
            .CheckAsync(
                new PlagiarismProviderRequest(
                    submission.Id.Value,
                    submission.TextContent,
                    submission.Files.Select(f => f.ArtifactId).ToList(),
                    check.Id.Value.ToString()),
                cancellationToken)
            .ConfigureAwait(false);

        if (providerResult.IsSuccess)
        {
            var score = PlagiarismScore.Create(providerResult.SimilarityPercentage, providerResult.MatchedSourceSummary, provider.ProviderName);
            if (score.IsSuccess)
            {
                check.Complete(score.Value, clock.UtcNow);
            }
            else
            {
                check.Fail($"The provider returned an unusable result: {score.Error!.Message}", clock.UtcNow);
            }
        }
        else
        {
            check.Fail(providerResult.Error ?? "The plagiarism-check provider could not be reached.", clock.UtcNow);
        }

        var assignment = await assignments.GetByIdAsync(submission.AssignmentId, cancellationToken).ConfigureAwait(false);
        submission.RaisePlagiarismOutcome(check, assignment?.CreatedByUserId ?? Guid.Empty, clock.UtcNow);

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }
}

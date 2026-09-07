using UMS.Modules.Learning.Domain.Submissions;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Learning.Domain.PlagiarismChecks;

/// <summary>
/// LRN-8/LRN-9: an async similarity check against one <see cref="Submission"/>'s content
/// (docs/ddd/ubiquitous-language.md). A child entity of <see cref="Submission"/> - it has no
/// lifecycle of its own outside the Submission it checks.
///
/// <para>
/// <b>This entity never gates anything.</b> requirement-spec.md §4's invariant: a Submission can be
/// evaluated regardless of check status (<c>Queued</c>/<c>Running</c>/<c>Completed</c>/
/// <c>Failed</c>) - the check is advisory evidence surfaced to the Instructor's own judgment,
/// mirroring ADR-0018's proctoring posture exactly. Nothing on <see cref="Submission.Evaluate"/>
/// consults this entity, deliberately.
/// </para>
///
/// <para>
/// Lifecycle per design-decisions.md's "PlagiarismCheck Execution Timing &amp; Resilience":
/// enqueued on <c>SubmissionCreated</c>; <see cref="Cancel"/>led if its Submission is superseded
/// before completion (so provider quota is not spent on moot content); re-enqueued at
/// <c>hardCloseAt</c> for any still-unchecked counted Submission; and transitioned to a real,
/// terminal <see cref="PlagiarismCheckStatus.Failed"/> - never silently "clean" - when the Polly
/// pipeline exhausts its retries or the circuit is open.
/// </para>
/// </summary>
public sealed class PlagiarismCheck
{
    private PlagiarismCheck()
    {
    }

    private PlagiarismCheck(PlagiarismCheckId id, SubmissionId submissionId, DateTimeOffset now)
    {
        Id = id;
        SubmissionId = submissionId;
        Status = PlagiarismCheckStatus.Queued;
        QueuedAt = now;
    }

    public PlagiarismCheckId Id { get; private init; }

    public SubmissionId SubmissionId { get; private init; }

    public PlagiarismCheckStatus Status { get; private set; }

    public PlagiarismScore? Score { get; private set; }

    public int AttemptCount { get; private set; }

    public DateTimeOffset QueuedAt { get; private set; }

    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>Populated on <see cref="PlagiarismCheckStatus.Failed"/> - the "could not be checked" text an Instructor sees, distinct from a low similarity score.</summary>
    public string? FailureReason { get; private set; }

    public bool IsTerminal => Status is PlagiarismCheckStatus.Completed or PlagiarismCheckStatus.Failed or PlagiarismCheckStatus.Cancelled;

    /// <summary>Claims this check for a worker pass. Increments <see cref="AttemptCount"/> so a repeatedly-failing provider is visible in the data, not only in the logs.</summary>
    public Result Start(DateTimeOffset now)
    {
        if (Status is not (PlagiarismCheckStatus.Queued or PlagiarismCheckStatus.Failed))
        {
            return Result.Failure(Error.Conflict("plagiarism_check.invalid_transition", $"Cannot start a PlagiarismCheck in status '{Status}'."));
        }

        Status = PlagiarismCheckStatus.Running;
        StartedAt = now;
        FailureReason = null;
        AttemptCount++;
        return Result.Success();
    }

    public Result Complete(PlagiarismScore score, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(score);

        if (Status != PlagiarismCheckStatus.Running)
        {
            return Result.Failure(Error.Conflict("plagiarism_check.invalid_transition", $"Cannot complete a PlagiarismCheck in status '{Status}' - only a Running check may complete."));
        }

        Status = PlagiarismCheckStatus.Completed;
        Score = score;
        CompletedAt = now;
        FailureReason = null;
        return Result.Success();
    }

    /// <summary>edge-cases.md "The plagiarism-check provider is slow or down": a real terminal status an Instructor can see and act on, never a silent "clean" result and never an indefinite "still checking".</summary>
    public Result Fail(string reason, DateTimeOffset now)
    {
        if (Status != PlagiarismCheckStatus.Running)
        {
            return Result.Failure(Error.Conflict("plagiarism_check.invalid_transition", $"Cannot fail a PlagiarismCheck in status '{Status}' - only a Running check may fail."));
        }

        Status = PlagiarismCheckStatus.Failed;
        CompletedAt = now;
        FailureReason = string.IsNullOrWhiteSpace(reason) ? "The plagiarism-check provider could not be reached." : reason.Trim();
        return Result.Success();
    }

    /// <summary>design-decisions.md: discarded when its Submission is superseded before the check completes. A check that already <see cref="PlagiarismCheckStatus.Completed"/> keeps its result - the evidence stays attached to the attempt it describes.</summary>
    public Result Cancel(DateTimeOffset now)
    {
        if (Status is PlagiarismCheckStatus.Completed or PlagiarismCheckStatus.Cancelled)
        {
            return Result.Failure(Error.Conflict("plagiarism_check.invalid_transition", $"Cannot cancel a PlagiarismCheck in status '{Status}'."));
        }

        Status = PlagiarismCheckStatus.Cancelled;
        CompletedAt = now;
        return Result.Success();
    }

    /// <summary>LRN-9: re-arms a Failed check, either from the <c>hardCloseAt</c> sweep or an Instructor's manual <c>POST .../plagiarism-check/retry</c>.</summary>
    public Result Requeue(DateTimeOffset now)
    {
        if (Status != PlagiarismCheckStatus.Failed)
        {
            return Result.Failure(Error.Conflict("plagiarism_check.invalid_transition", $"Cannot requeue a PlagiarismCheck in status '{Status}' - only a Failed check may be retried."));
        }

        Status = PlagiarismCheckStatus.Queued;
        QueuedAt = now;
        StartedAt = null;
        CompletedAt = null;
        return Result.Success();
    }

    internal static PlagiarismCheck Enqueue(SubmissionId submissionId, DateTimeOffset now) =>
        new(PlagiarismCheckId.New(), submissionId, now);
}

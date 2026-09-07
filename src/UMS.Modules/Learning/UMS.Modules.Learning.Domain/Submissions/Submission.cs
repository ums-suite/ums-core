using UMS.Modules.Learning.Domain.Assignments;
using UMS.Modules.Learning.Domain.Common;
using UMS.Modules.Learning.Domain.Events;
using UMS.Modules.Learning.Domain.PlagiarismChecks;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Learning.Domain.Submissions;

/// <summary>
/// LRN-6/LRN-7/LRN-11: one Student's one attempt against a <c>Published</c>
/// <see cref="Assignment"/> (docs/ddd/ubiquitous-language.md).
///
/// <para>
/// <b>Immutable content, with an explicit superseded chain.</b> requirement-spec.md §4: a
/// Submission's content is never mutated in place. A resubmission creates a NEW Submission and
/// marks the previous one <see cref="SubmissionStatus.Superseded"/> in the same transaction; the
/// latest non-superseded row is the one graded and plagiarism-checked, but every prior attempt
/// remains permanently retrievable. This is a direct reuse of Documents'
/// <c>GeneratedDocument.Supersede</c> shape, not a third correction primitive invented here
/// (design-decisions.md, "Submission Immutability &amp; Resubmission via Superseded-Chain
/// Pattern").
/// </para>
///
/// <para>
/// <see cref="SubmittedAt"/> is ALWAYS the server's own receipt timestamp, supplied by the
/// application layer's <c>IClock</c> - there is deliberately no constructor parameter, request
/// field, or setter anywhere on this type through which a client-reported timestamp could reach it
/// (requirement-spec.md §4's server-authoritative invariant, enforced by shape rather than by
/// validation).
/// </para>
///
/// <para>
/// <b>Nothing here calls Academic.</b> <see cref="Evaluate"/> raises
/// <see cref="SubmissionEvaluated"/> as a one-directional fan-out event only; there is no
/// Academic-facing write dependency in this aggregate, this module's Application layer, or its
/// Infrastructure layer (design-decisions.md, "Cross-Module Feed of Assignment Scores into
/// Academic's Grade" - the mechanism keeping module-boundaries.md's acyclic graph intact).
/// </para>
/// </summary>
public sealed class Submission : AggregateRoot<SubmissionId>
{
    private readonly List<SubmissionFile> _files = [];
    private readonly List<PlagiarismCheck> _plagiarismChecks = [];

    private Submission()
    {
    }

    private Submission(
        SubmissionId id,
        AssignmentId assignmentId,
        Guid courseOfferingId,
        Guid studentId,
        Guid submittedByUserId,
        string? textContent,
        IReadOnlyCollection<SubmissionFile> files,
        DateTimeOffset submittedAt,
        bool isLate,
        decimal latePenaltyPercentage,
        SubmissionExtensionId? appliedExtensionId)
    {
        Id = id;
        AssignmentId = assignmentId;
        CourseOfferingId = courseOfferingId;
        StudentId = studentId;
        SubmittedByUserId = submittedByUserId;
        TextContent = textContent;
        _files.AddRange(files);
        SubmittedAt = submittedAt;
        IsLate = isLate;
        LatePenaltyPercentage = latePenaltyPercentage;
        AppliedExtensionId = appliedExtensionId;
        Status = SubmissionStatus.Submitted;
    }

    public AssignmentId AssignmentId { get; private set; }

    /// <summary>Denormalized from the Assignment purely so the Instructor's grading queue and the ownership check can scope by offering without a second read - never a second source of truth.</summary>
    public Guid CourseOfferingId { get; private set; }

    public Guid StudentId { get; private set; }

    /// <summary>The Identity <c>User</c> who submitted - carried so the <c>SubmissionEvaluated</c> fan-out has a notification recipient without a second cross-module hop.</summary>
    public Guid SubmittedByUserId { get; private set; }

    public string? TextContent { get; private set; }

    /// <summary>The server's own receipt-of-complete-payload instant. Never client-supplied (requirement-spec.md §4).</summary>
    public DateTimeOffset SubmittedAt { get; private set; }

    public bool IsLate { get; private set; }

    /// <summary>0-100, resolved from the Assignment's tiered <c>LatePenaltyPolicy</c> at submission time and frozen here, so a later policy edit never silently re-scores an already-submitted attempt.</summary>
    public decimal LatePenaltyPercentage { get; private set; }

    public SubmissionExtensionId? AppliedExtensionId { get; private set; }

    public SubmissionStatus Status { get; private set; }

    public SubmissionId? SupersededBySubmissionId { get; private set; }

    public DateTimeOffset? SupersededAt { get; private set; }

    public AssignmentScore? Score { get; private set; }

    public Guid? EvaluatedByUserId { get; private set; }

    public DateTimeOffset? EvaluatedAt { get; private set; }

    public IReadOnlyCollection<SubmissionFile> Files => _files.AsReadOnly();

    public IReadOnlyCollection<PlagiarismCheck> PlagiarismChecks => _plagiarismChecks.AsReadOnly();

    /// <summary>The check that speaks for this Submission right now - the most recently enqueued one. <see langword="null"/> until the dispatch worker has picked up the <c>SubmissionCreated</c> event.</summary>
    public PlagiarismCheck? LatestPlagiarismCheck => _plagiarismChecks.Count == 0 ? null : _plagiarismChecks[^1];

    /// <summary>
    /// LRN-6. <paramref name="acceptance"/> must be an ACCEPTED verdict from
    /// <see cref="Assignment.EvaluateAcceptance"/> computed against the same
    /// <paramref name="submittedAt"/> - the aggregate re-asserts that rather than trusting the
    /// caller silently, so an accept check can never be skipped by a future call site.
    /// </summary>
    public static Result<Submission> Create(
        Assignment assignment,
        Guid studentId,
        Guid submittedByUserId,
        string? textContent,
        IReadOnlyCollection<SubmissionFile> files,
        SubmissionAcceptance acceptance,
        DateTimeOffset submittedAt)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(acceptance);

        if (!acceptance.IsAccepted)
        {
            return Error.Conflict(acceptance.RejectionCode!, acceptance.RejectionMessage!);
        }

        var hasText = !string.IsNullOrWhiteSpace(textContent);
        var hasFiles = files.Count > 0;

        if (!hasText && !hasFiles)
        {
            return Error.Validation("submission.empty", "A Submission must carry text content, at least one file, or both.");
        }

        switch (assignment.AllowedSubmissionType)
        {
            case AllowedSubmissionType.Text when hasFiles:
                return Error.Validation("submission.files_not_allowed", $"Assignment '{assignment.Id}' accepts text submissions only.");
            case AllowedSubmissionType.Text when !hasText:
                return Error.Validation("submission.text_required", $"Assignment '{assignment.Id}' requires text content.");
            case AllowedSubmissionType.File when !hasFiles:
                return Error.Validation("submission.file_required", $"Assignment '{assignment.Id}' requires at least one uploaded file.");
            case AllowedSubmissionType.TextOrFile:
            default:
                break;
        }

        var submission = new Submission(
            SubmissionId.New(),
            assignment.Id,
            assignment.CourseOfferingId,
            studentId,
            submittedByUserId,
            hasText ? textContent!.Trim() : null,
            files,
            submittedAt,
            acceptance.IsLate,
            acceptance.LatePenaltyPercentage,
            acceptance.AppliedExtensionId);

        submission.Raise(new SubmissionCreated(
            submission.Id.Value,
            assignment.Id.Value,
            assignment.CourseOfferingId,
            studentId,
            submittedByUserId,
            submittedAt,
            acceptance.IsLate,
            acceptance.LatePenaltyPercentage,
            submittedAt));

        return submission;
    }

    /// <summary>
    /// LRN-7: marks this attempt superseded by <paramref name="replacementId"/>. Content is never
    /// touched - the row stays permanently retrievable as the evidence of what this Student
    /// actually submitted and when (edge-cases.md, "A Student submits twice").
    /// </summary>
    public Result Supersede(SubmissionId replacementId, DateTimeOffset now)
    {
        if (Status != SubmissionStatus.Submitted)
        {
            return Result.Failure(Error.Conflict("submission.invalid_transition", $"Cannot supersede a Submission in status '{Status}'."));
        }

        Status = SubmissionStatus.Superseded;
        SupersededBySubmissionId = replacementId;
        SupersededAt = now;

        // design-decisions.md "PlagiarismCheck Execution Timing & Resilience": a check whose
        // Submission is superseded before it completes is discarded rather than run to completion,
        // so metered provider quota is not spent on content a resubmission already made moot. An
        // already-Completed check keeps its result - the evidence stays attached to the attempt it
        // describes.
        foreach (var check in _plagiarismChecks.Where(c => !c.IsTerminal))
        {
            check.Cancel(now);
        }

        Raise(new SubmissionSuperseded(Id.Value, replacementId.Value, AssignmentId.Value, StudentId, now));
        return Result.Success();
    }

    /// <summary>
    /// LRN-11: records an <see cref="AssignmentScore"/> and publishes <see cref="SubmissionEvaluated"/>.
    ///
    /// <para>
    /// <b>Deliberately does not consult <see cref="PlagiarismChecks"/> at all.</b>
    /// requirement-spec.md §4: "A Submission can be evaluated regardless of PlagiarismCheck status" -
    /// including <c>Failed</c>. Blocking here would let a third-party vendor's uptime dictate
    /// whether Learning's grading pipeline can proceed, and would silently violate ADR-0018's
    /// "flag, don't auto-decide" posture even while nominally satisfying "never auto-rejects".
    /// </para>
    /// </summary>
    public Result Evaluate(decimal points, int maxPoints, string? feedback, Guid evaluatedByUserId, DateTimeOffset now)
    {
        if (Status == SubmissionStatus.Superseded)
        {
            return Result.Failure(Error.Conflict("submission.superseded", "This Submission has been superseded by a later attempt - evaluate that one instead."));
        }

        var score = AssignmentScore.Create(points, maxPoints, LatePenaltyPercentage, feedback);
        if (score.IsFailure)
        {
            return Result.Failure(score.Error!);
        }

        Score = score.Value;
        EvaluatedByUserId = evaluatedByUserId;
        EvaluatedAt = now;

        Raise(new SubmissionEvaluated(
            Id.Value,
            AssignmentId.Value,
            CourseOfferingId,
            StudentId,
            SubmittedByUserId,
            score.Value.AwardedPoints,
            score.Value.MaxPoints,
            evaluatedByUserId,
            now));

        return Result.Success();
    }

    /// <summary>LRN-8: enqueues a check for this Submission. Refuses to stack a second live check on top of an existing non-terminal one, so a duplicated <c>SubmissionCreated</c> relay pass is a no-op rather than a double provider call.</summary>
    public Result<PlagiarismCheck> EnqueuePlagiarismCheck(DateTimeOffset now)
    {
        if (Status == SubmissionStatus.Superseded)
        {
            return Error.Conflict("plagiarism_check.submission_superseded", "A superseded Submission is never plagiarism-checked - the attempt that supersedes it is.");
        }

        if (_plagiarismChecks.Find(c => !c.IsTerminal) is { } inFlight)
        {
            return inFlight;
        }

        if (_plagiarismChecks.Find(c => c.Status == PlagiarismCheckStatus.Completed) is not null)
        {
            return Error.Conflict("plagiarism_check.already_completed", "This Submission already has a completed PlagiarismCheck.");
        }

        var check = PlagiarismCheck.Enqueue(Id, now);
        _plagiarismChecks.Add(check);
        return check;
    }

    /// <summary>LRN-9: re-arms the latest Failed check, or enqueues a fresh one if none exists yet. Used by both the <c>hardCloseAt</c> sweep and the Instructor's manual retry endpoint.</summary>
    public Result<PlagiarismCheck> RetryPlagiarismCheck(DateTimeOffset now)
    {
        var latest = LatestPlagiarismCheck;
        if (latest is null)
        {
            return EnqueuePlagiarismCheck(now);
        }

        if (latest.Status == PlagiarismCheckStatus.Failed)
        {
            var requeued = latest.Requeue(now);
            return requeued.IsFailure ? requeued.Error! : latest;
        }

        return latest.IsTerminal
            ? Error.Conflict("plagiarism_check.not_retryable", $"The latest PlagiarismCheck for this Submission is in terminal status '{latest.Status}' and cannot be retried.")
            : latest;
    }

    /// <summary>Raises the terminal fan-out event for whichever outcome the provider call produced - called by the application layer once it has driven the check's own state transition.</summary>
    public void RaisePlagiarismOutcome(PlagiarismCheck check, Guid instructorUserId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(check);

        if (check.Status == PlagiarismCheckStatus.Completed && check.Score is { } score)
        {
            Raise(new PlagiarismCheckCompleted(check.Id.Value, Id.Value, AssignmentId.Value, score.SimilarityPercentage, score.ProviderName, instructorUserId, now));
        }
        else if (check.Status == PlagiarismCheckStatus.Failed)
        {
            Raise(new PlagiarismCheckFailed(check.Id.Value, Id.Value, AssignmentId.Value, check.FailureReason ?? "unknown", check.AttemptCount, instructorUserId, now));
        }
    }
}

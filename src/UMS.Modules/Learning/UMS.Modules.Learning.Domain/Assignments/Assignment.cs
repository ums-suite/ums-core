using UMS.Modules.Learning.Domain.Common;
using UMS.Modules.Learning.Domain.Events;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Learning.Domain.Assignments;

/// <summary>
/// LRN-1/LRN-2/LRN-3: a gradable task published against Academic's <c>CourseOffering</c>
/// (docs/ddd/ubiquitous-language.md), owning its <see cref="SubmissionWindow"/> and every
/// per-Student <see cref="SubmissionExtension"/> granted against it.
///
/// <para>
/// The <c>CourseOffering</c> reference is a plain <see cref="Guid"/> by design: it is another
/// module's aggregate id, resolved through <c>UMS.Shared.Academic.ICourseOfferingLookup</c>, and
/// this module must not declare a strongly-typed id it does not own (ums-conventions.md's
/// strongly-typed-id rule governs ids crossing THIS module's own aggregate boundaries - Academic's
/// <c>CourseOfferingId</c> lives in Academic's Domain layer, which module-boundaries.md forbids
/// referencing). Same reasoning for <c>StudentId</c> on <see cref="SubmissionExtension"/>.
/// </para>
///
/// <para>
/// <b>Instructor authorization is NOT enforced here.</b> The caller (<c>AssignmentService</c>) must
/// have already verified, against <c>ICourseOfferingLookup.IsInstructorForOfferingAsync</c>, that
/// the actor owns this <c>CourseOffering</c> - the aggregate never reaches across modules itself,
/// exactly as Academic's own <c>CourseOffering.AssignInstructor</c> documents for the identical
/// split.
/// </para>
/// </summary>
public sealed class Assignment : AggregateRoot<AssignmentId>
{
    private readonly List<SubmissionExtension> _extensions = [];

    private Assignment()
    {
    }

    private Assignment(
        AssignmentId id,
        Guid courseOfferingId,
        string title,
        string instructions,
        AllowedSubmissionType allowedSubmissionType,
        bool allowResubmission,
        int maxPoints,
        SubmissionWindow submissionWindow,
        Guid createdByUserId,
        DateTimeOffset now)
    {
        Id = id;
        CourseOfferingId = courseOfferingId;
        Title = title;
        Instructions = instructions;
        AllowedSubmissionType = allowedSubmissionType;
        AllowResubmission = allowResubmission;
        MaxPoints = maxPoints;
        SubmissionWindow = submissionWindow;
        CreatedByUserId = createdByUserId;
        Status = AssignmentStatus.Draft;
        CreatedAt = now;
    }

    public Guid CourseOfferingId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Instructions { get; private set; } = string.Empty;

    public AllowedSubmissionType AllowedSubmissionType { get; private set; }

    /// <summary>edge-cases.md "A Student submits twice" residual note: an Assignment with resubmission disabled rejects a second submission outright rather than creating a superseding attempt.</summary>
    public bool AllowResubmission { get; private set; }

    public int MaxPoints { get; private set; }

    public SubmissionWindow SubmissionWindow { get; private set; } = null!;

    /// <summary>The Identity <c>User</c> who created (and, per §6's ownership rule, owns) this Assignment - the Instructor's own user id, carried on fan-out events so a notification recipient needs no second lookup.</summary>
    public Guid CreatedByUserId { get; private set; }

    public AssignmentStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? PublishedAt { get; private set; }

    public DateTimeOffset? ClosedAt { get; private set; }

    public DateTimeOffset? CancelledAt { get; private set; }

    public string? CancellationReason { get; private set; }

    public IReadOnlyCollection<SubmissionExtension> Extensions => _extensions.AsReadOnly();

    public static Result<Assignment> Create(
        Guid courseOfferingId,
        string title,
        string instructions,
        AllowedSubmissionType allowedSubmissionType,
        bool allowResubmission,
        int maxPoints,
        SubmissionWindow submissionWindow,
        Guid createdByUserId,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(submissionWindow);

        if (string.IsNullOrWhiteSpace(title))
        {
            return Error.Validation("assignment.title_required", "An Assignment title is required.");
        }

        return maxPoints < 1
            ? Error.Validation("assignment.max_points_out_of_range", "An Assignment's maxPoints must be at least 1.")
            : new Assignment(
                AssignmentId.New(),
                courseOfferingId,
                title.Trim(),
                (instructions ?? string.Empty).Trim(),
                allowedSubmissionType,
                allowResubmission,
                maxPoints,
                submissionWindow,
                createdByUserId,
                now);
    }

    /// <summary>LRN-1: <c>Draft -&gt; Published</c>. Only a Published Assignment accepts Submissions (requirement-spec.md §2).</summary>
    public Result Publish(DateTimeOffset now)
    {
        if (Status != AssignmentStatus.Draft)
        {
            return Result.Failure(Error.Conflict("assignment.invalid_transition", $"Cannot publish an Assignment in status '{Status}' - only a Draft may be published."));
        }

        Status = AssignmentStatus.Published;
        PublishedAt = now;
        Raise(new AssignmentPublished(Id.Value, CourseOfferingId, Title, SubmissionWindow.Deadline, CreatedByUserId, now));
        return Result.Success();
    }

    /// <summary>LRN-2: manual close ahead of <c>hardCloseAt</c>, or the automatic close a background sweep performs once <c>hardCloseAt</c> is reached.</summary>
    public Result Close(DateTimeOffset now)
    {
        if (Status != AssignmentStatus.Published)
        {
            return Result.Failure(Error.Conflict("assignment.invalid_transition", $"Cannot close an Assignment in status '{Status}' - only a Published Assignment may be closed."));
        }

        Status = AssignmentStatus.Closed;
        ClosedAt = now;
        Raise(new AssignmentClosed(Id.Value, CourseOfferingId, CreatedByUserId, now));
        return Result.Success();
    }

    /// <summary>
    /// LRN-2 / edge-cases.md "A CourseOffering is cancelled mid-semester": the manual first-pass
    /// answer, since Academic defines no <c>CourseOfferingCancelled</c> event for Learning to
    /// subscribe to. Submissions already collected stay retained and readable - cancelling stops
    /// intake going forward, it never retroactively invalidates grading work already done.
    /// </summary>
    public Result Cancel(string reason, DateTimeOffset now)
    {
        if (Status is AssignmentStatus.Cancelled)
        {
            return Result.Failure(Error.Conflict("assignment.invalid_transition", "This Assignment is already cancelled."));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure(Error.Validation("assignment.cancellation_reason_required", "A cancellation reason is required."));
        }

        Status = AssignmentStatus.Cancelled;
        CancelledAt = now;
        CancellationReason = reason.Trim();
        Raise(new AssignmentCancelled(Id.Value, CourseOfferingId, CancellationReason, CreatedByUserId, now));
        return Result.Success();
    }

    /// <summary>LRN-3: grants a per-Student accommodation. One live extension per <c>(Assignment, Student)</c> - a re-grant replaces the prior one rather than stacking, so "the later of base window and extension" stays unambiguous.</summary>
    public Result<SubmissionExtension> GrantExtension(
        Guid studentId,
        Guid studentUserId,
        DateTimeOffset extendedDeadline,
        Guid grantedByUserId,
        string reason,
        bool waivesLatePenalty,
        DateTimeOffset now)
    {
        if (Status is AssignmentStatus.Cancelled)
        {
            return Error.Conflict("assignment.extension_on_cancelled_assignment", "Cannot grant a SubmissionExtension on a cancelled Assignment.");
        }

        if (Status is AssignmentStatus.Draft)
        {
            return Error.Conflict("assignment.extension_on_draft_assignment", "Cannot grant a SubmissionExtension on an Assignment that has not been published yet.");
        }

        var created = SubmissionExtension.Create(Id, studentId, studentUserId, extendedDeadline, grantedByUserId, reason, waivesLatePenalty, now);
        if (created.IsFailure)
        {
            return created.Error!;
        }

        _extensions.RemoveAll(e => e.StudentId == studentId);
        _extensions.Add(created.Value);
        Raise(new AssignmentExtensionGranted(Id.Value, created.Value.Id.Value, studentId, studentUserId, extendedDeadline, grantedByUserId, waivesLatePenalty, now));
        return created.Value;
    }

    public SubmissionExtension? ExtensionFor(Guid studentId) => _extensions.Find(e => e.StudentId == studentId);

    /// <summary>
    /// LRN-6, requirement-spec.md §4/§5 and design-decisions.md's "Why Submission Timing Doesn't
    /// Need Seat-Limit-Style Concurrency Control": the whole accept/reject decision, as a stateless
    /// comparison of the SERVER's own receipt timestamp against static, already-computed values.
    /// No lock, no shared counter, no <c>SELECT ... FOR UPDATE</c> - there is no scarce resource a
    /// Submission contends over, unlike Academic's <c>CourseOffering.enrolled_count</c>.
    ///
    /// <para>
    /// Evaluation order, as design-decisions.md's "Late-Penalty &amp; Grace-Period Model" fixes it:
    /// status gate, then window-open gate, then <c>deadline + gracePeriod</c>, then the tier
    /// schedule, then <c>hardCloseAt</c> - with any applicable <see cref="SubmissionExtension"/>
    /// taking the LATER of its own <c>extendedDeadline</c> and the base window's hard close, and
    /// waiving the penalty entirely when the grant said so.
    /// </para>
    /// </summary>
    /// <param name="submittedAt">The server's own receipt-of-complete-payload instant. Never a client-supplied value (requirement-spec.md §4).</param>
    /// <param name="studentId">Whose extension, if any, applies. Every other Student's evaluation is completely unaffected by it.</param>
    public SubmissionAcceptance EvaluateAcceptance(DateTimeOffset submittedAt, Guid studentId)
    {
        var extension = ExtensionFor(studentId);

        // A Closed Assignment still accepts a submission from a Student whose own
        // SubmissionExtension is live at this instant - that is precisely the accommodation case
        // requirement-spec.md §2 names ("reopen the window for one Student only ... without
        // reopening it for everyone"), and an Assignment reaches Closed automatically the moment
        // hardCloseAt passes, so an Instructor granting an accommodation afterwards must not find it
        // silently void. Draft and Cancelled accept nothing from anyone, extension or not.
        var isOpenForThisStudent = Status == AssignmentStatus.Published
            || (Status == AssignmentStatus.Closed && extension is not null && submittedAt <= extension.ExtendedDeadline);

        if (!isOpenForThisStudent)
        {
            return SubmissionAcceptance.Rejected(
                "submission.assignment_not_published",
                $"Assignment '{Id}' is in status '{Status}' and is not accepting submissions.");
        }

        if (submittedAt < SubmissionWindow.OpensAt)
        {
            return SubmissionAcceptance.Rejected(
                "submission.window_not_open",
                $"Assignment '{Id}' does not open for submissions until {SubmissionWindow.OpensAt:O}.");
        }

        var effectiveHardClose = extension is null
            ? SubmissionWindow.HardCloseAt
            : Max(SubmissionWindow.HardCloseAt, extension.ExtendedDeadline);

        if (submittedAt > effectiveHardClose)
        {
            return SubmissionAcceptance.Rejected(
                "submission.window_closed",
                $"Assignment '{Id}' stopped accepting submissions at {effectiveHardClose:O}.");
        }

        var lateness = submittedAt - SubmissionWindow.EffectiveDeadline;
        if (lateness <= TimeSpan.Zero)
        {
            return SubmissionAcceptance.Accepted(isLate: false, TimeSpan.Zero, 0m, extension?.Id);
        }

        var penalty = extension?.WaivesLatePenalty == true
            ? 0m
            : SubmissionWindow.LatePenaltyPolicy.DeductionFor(lateness);

        return SubmissionAcceptance.Accepted(isLate: true, lateness, penalty, extension?.Id);
    }

    private static DateTimeOffset Max(DateTimeOffset left, DateTimeOffset right) => left >= right ? left : right;
}

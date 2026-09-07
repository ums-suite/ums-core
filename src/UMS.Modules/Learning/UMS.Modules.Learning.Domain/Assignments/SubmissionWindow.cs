using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Learning.Domain.Assignments;

/// <summary>
/// requirement-spec.md learning §3's value object: the opening instant, the <see cref="Deadline"/>,
/// a <see cref="GracePeriod"/>, a <see cref="LatePenaltyPolicy"/>, and a <see cref="HardCloseAt"/>
/// past which no submission is accepted at all, regardless of penalty.
///
/// <para>
/// <b>The grace period is folded INTO the effective deadline, not evaluated as a separate later
/// rule</b> (design-decisions.md's "Late-Penalty &amp; Grace-Period Model"): "the tiered-penalty
/// comparison and the grace-period comparison are a single, ordered evaluation, not two independent
/// rules that could disagree". <see cref="EffectiveDeadline"/> is that single value; the
/// Student-facing UI shows <see cref="Deadline"/>, so the grace period stays a safety margin
/// against ordinary network variance rather than something a Student is invited to plan around
/// (edge-cases.md, "A Submission upload starts before the deadline and finishes after it").
/// </para>
/// </summary>
public sealed record SubmissionWindow
{
    private SubmissionWindow(DateTimeOffset opensAt, DateTimeOffset deadline, TimeSpan gracePeriod, LatePenaltyPolicy latePenaltyPolicy, DateTimeOffset hardCloseAt)
    {
        OpensAt = opensAt;
        Deadline = deadline;
        GracePeriod = gracePeriod;
        LatePenaltyPolicy = latePenaltyPolicy;
        HardCloseAt = hardCloseAt;
    }

    public DateTimeOffset OpensAt { get; }

    /// <summary>The deadline a Student is shown. Never the value the accept check itself compares against - see <see cref="EffectiveDeadline"/>.</summary>
    public DateTimeOffset Deadline { get; }

    public TimeSpan GracePeriod { get; }

    public LatePenaltyPolicy LatePenaltyPolicy { get; }

    /// <summary>Past this instant nothing is accepted at any penalty level - the one hard boundary a base window imposes (a per-Student <see cref="SubmissionExtension"/> may move it for that Student alone).</summary>
    public DateTimeOffset HardCloseAt { get; }

    /// <summary><see cref="Deadline"/> + <see cref="GracePeriod"/> - the single value on-time-vs-late is decided by.</summary>
    public DateTimeOffset EffectiveDeadline => Deadline + GracePeriod;

    public static Result<SubmissionWindow> Create(
        DateTimeOffset opensAt,
        DateTimeOffset deadline,
        TimeSpan gracePeriod,
        LatePenaltyPolicy latePenaltyPolicy,
        DateTimeOffset hardCloseAt)
    {
        ArgumentNullException.ThrowIfNull(latePenaltyPolicy);

        if (deadline <= opensAt)
        {
            return Error.Validation("submission_window.deadline_before_open", "A SubmissionWindow's deadline must be after the instant it opens.");
        }

        if (gracePeriod < TimeSpan.Zero)
        {
            return Error.Validation("submission_window.negative_grace_period", "A SubmissionWindow's gracePeriod cannot be negative.");
        }

        return hardCloseAt < deadline + gracePeriod
            ? Error.Validation("submission_window.hard_close_before_effective_deadline", "A SubmissionWindow's hardCloseAt must not precede its effective deadline (deadline + gracePeriod).")
            : new SubmissionWindow(opensAt, deadline, gracePeriod, latePenaltyPolicy, hardCloseAt);
    }
}

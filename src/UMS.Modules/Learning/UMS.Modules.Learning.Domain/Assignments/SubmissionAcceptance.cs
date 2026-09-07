namespace UMS.Modules.Learning.Domain.Assignments;

/// <summary>
/// The verdict <see cref="Assignment.EvaluateAcceptance"/> returns for one prospective
/// <c>Submission</c> at one server-observed instant - a pure function of that instant, the
/// <see cref="SubmissionWindow"/>, and (optionally) that Student's own
/// <see cref="SubmissionExtension"/>.
///
/// <para>
/// Deliberately a stateless computation returning a value, not a mutation of shared state:
/// design-decisions.md's "Why Submission Timing Doesn't Need Seat-Limit-Style Concurrency Control"
/// resolves that there is no counter, no capacity, and therefore nothing for two concurrent
/// submissions to race over. Two callers evaluating this at the same instant get the same answer
/// and neither invalidates the other.
/// </para>
/// </summary>
/// <param name="IsAccepted">Whether a Submission may be created at all.</param>
/// <param name="RejectionCode">Machine-readable rejection reason; <see langword="null"/> when accepted.</param>
/// <param name="RejectionMessage">Human-readable rejection reason; <see langword="null"/> when accepted.</param>
/// <param name="IsLate">Whether the submission landed after the effective deadline (deadline + grace period).</param>
/// <param name="Lateness">How far past the effective deadline the submission landed; <see cref="TimeSpan.Zero"/> when on time.</param>
/// <param name="LatePenaltyPercentage">0-100, resolved from the tiered <see cref="LatePenaltyPolicy"/> - always 0 when a <see cref="SubmissionExtension"/> with <c>waivesLatePenalty</c> applies.</param>
/// <param name="AppliedExtensionId">The <see cref="SubmissionExtension"/> that widened acceptance for this Student, if any.</param>
public sealed record SubmissionAcceptance(
    bool IsAccepted,
    string? RejectionCode,
    string? RejectionMessage,
    bool IsLate,
    TimeSpan Lateness,
    decimal LatePenaltyPercentage,
    SubmissionExtensionId? AppliedExtensionId)
{
    internal static SubmissionAcceptance Rejected(string code, string message) =>
        new(false, code, message, false, TimeSpan.Zero, 0m, null);

    internal static SubmissionAcceptance Accepted(bool isLate, TimeSpan lateness, decimal latePenaltyPercentage, SubmissionExtensionId? appliedExtensionId) =>
        new(true, null, null, isLate, lateness, latePenaltyPercentage, appliedExtensionId);
}

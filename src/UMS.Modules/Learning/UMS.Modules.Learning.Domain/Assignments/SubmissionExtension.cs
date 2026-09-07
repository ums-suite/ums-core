using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Learning.Domain.Assignments;

/// <summary>
/// requirement-spec.md learning §3 (module-local, pending glossary merge) and design-decisions.md's
/// "Per-Student Submission Extension (Accommodation) Design": a first-class, audited override of an
/// <see cref="Assignment"/>'s <see cref="SubmissionWindow"/> scoped to exactly one
/// <c>(Assignment, Student)</c> pair - never a widening of the shared window, which would have no
/// structural guarantee against another Student slipping in during the widened period.
///
/// <para>
/// <see cref="WaivesLatePenalty"/> is edge-cases.md's own residual note made explicit: a genuine
/// accommodation and an "I'll give you a few extra hours but it's still late" grant are both
/// legitimate, distinct Instructor intents this module must not collapse into one fixed rule.
/// </para>
/// </summary>
public sealed class SubmissionExtension
{
    private SubmissionExtension()
    {
    }

    private SubmissionExtension(
        SubmissionExtensionId id,
        AssignmentId assignmentId,
        Guid studentId,
        Guid studentUserId,
        DateTimeOffset extendedDeadline,
        Guid grantedByUserId,
        string reason,
        bool waivesLatePenalty,
        DateTimeOffset grantedAt)
    {
        Id = id;
        AssignmentId = assignmentId;
        StudentId = studentId;
        StudentUserId = studentUserId;
        ExtendedDeadline = extendedDeadline;
        GrantedByUserId = grantedByUserId;
        Reason = reason;
        WaivesLatePenalty = waivesLatePenalty;
        GrantedAt = grantedAt;
    }

    public SubmissionExtensionId Id { get; private init; }

    public AssignmentId AssignmentId { get; private init; }

    public Guid StudentId { get; private init; }

    /// <summary>The Identity <c>User</c> behind that Student, captured at grant time so the <c>AssignmentExtensionGranted</c> fan-out has a recipient without a second cross-module hop.</summary>
    public Guid StudentUserId { get; private init; }

    /// <summary>Replaces <c>hardCloseAt</c> for this Student alone; the accept check takes the later of this and the base window's own hard close.</summary>
    public DateTimeOffset ExtendedDeadline { get; private init; }

    public Guid GrantedByUserId { get; private init; }

    public string Reason { get; private init; } = string.Empty;

    /// <summary>When true, a submission accepted under this extension carries no late-penalty deduction at all, regardless of how far past the base effective deadline it lands.</summary>
    public bool WaivesLatePenalty { get; private init; }

    public DateTimeOffset GrantedAt { get; private init; }

    internal static Result<SubmissionExtension> Create(
        AssignmentId assignmentId,
        Guid studentId,
        Guid studentUserId,
        DateTimeOffset extendedDeadline,
        Guid grantedByUserId,
        string reason,
        bool waivesLatePenalty,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Error.Validation("submission_extension.reason_required", "A SubmissionExtension must record why it was granted - a fairness-affecting accommodation is never an unexplained mutation.");
        }

        return extendedDeadline <= now
            ? Error.Validation("submission_extension.deadline_in_the_past", "A SubmissionExtension's extendedDeadline must be in the future.")
            : new SubmissionExtension(SubmissionExtensionId.New(), assignmentId, studentId, studentUserId, extendedDeadline, grantedByUserId, reason.Trim(), waivesLatePenalty, now);
    }
}

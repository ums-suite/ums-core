using UMS.Modules.Academic.Domain.Common;

namespace UMS.Modules.Academic.Domain.ResultPublications;

/// <summary>
/// ACD-10..13: the controlled state machine governing when a Grade/GPA becomes visible to a
/// Student (docs/ddd/ubiquitous-language.md) - one ResultPublication per CourseOffering (the
/// "grade batch" edge-cases.md's concurrent-reviewer race refers to).
///
/// <para>
/// <b>Concurrency mechanism - read this before changing any transition method.</b>
/// design-decisions.md's "Grade-Lock State Machine Design" mandates state-guarded conditional
/// updates, NOT a version column, for every transition. This class's own transition methods
/// (<see cref="Submit"/>, <see cref="Reject"/>, <see cref="Lock"/>, <see cref="Approve"/>,
/// <see cref="Publish"/>, <see cref="Archive"/>, <see cref="ReenterForCorrection"/>) validate legal-
/// transition rules purely in-memory (useful on their own for unit tests and for constructing a
/// good error message) - they do NOT, by themselves, provide the race-proof guarantee two
/// concurrent writers need. That guarantee comes from
/// <c>ResultPublicationRepository.TryTransitionAsync</c>, which issues the actual persistence as
/// a single <c>UPDATE ... WHERE id = @id AND status = @expectedPriorStatus</c> statement
/// (<c>ExecuteUpdateAsync</c>, bypassing EF's change tracker entirely, mirroring
/// <c>CourseOffering</c>'s own seat-limit mechanism). A losing writer's conditional update affects
/// zero rows; the caller (<c>ResultPublicationService</c>) re-reads the row's actual current
/// status and returns an explicit, named rejection identifying it - never a silent overwrite,
/// never a generic conflict error (edge-cases.md, "Grade lock racing a still-in-flight grade
/// submission" and "Concurrent Department-Head review/approval of the same grade batch").
/// </para>
/// </summary>
public sealed class ResultPublication : AggregateRoot<ResultPublicationId>
{
    private ResultPublication()
    {
    }

    private ResultPublication(ResultPublicationId id, Guid courseOfferingId, DateTimeOffset now)
    {
        Id = id;
        CourseOfferingId = courseOfferingId;
        Status = ResultPublicationStatus.Draft;
        CreatedAt = now;
    }

    public Guid CourseOfferingId { get; private set; }

    public ResultPublicationStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? CalculatedAt { get; private set; }

    public DateTimeOffset? RejectedAt { get; private set; }

    public Guid? RejectedByUserId { get; private set; }

    public string? RejectionReason { get; private set; }

    public DateTimeOffset? LockedAt { get; private set; }

    public Guid? LockedByUserId { get; private set; }

    public DateTimeOffset? ApprovedAt { get; private set; }

    public Guid? ApprovedByUserId { get; private set; }

    public DateTimeOffset? PublishedAt { get; private set; }

    public Guid? PublishedByUserId { get; private set; }

    public DateTimeOffset? ArchivedAt { get; private set; }

    public int CorrectionCount { get; private set; }

    public static ResultPublication Create(Guid courseOfferingId, DateTimeOffset now) => new(ResultPublicationId.New(), courseOfferingId, now);

    /// <summary>ACD-10: Faculty submits/resubmits marks for this batch. Legal from `Draft` (first submission) or `Calculated` (any resubmission before lock) - explicitly illegal once `Verified`/`Approved`/`Published`/`Archived`.</summary>
    public void Submit(DateTimeOffset now)
    {
        if (Status is not (ResultPublicationStatus.Draft or ResultPublicationStatus.Calculated))
        {
            throw new InvalidOperationException($"Cannot submit grades for a ResultPublication in status '{Status}' - a grade batch is only submittable while 'Draft' or 'Calculated'.");
        }

        Status = ResultPublicationStatus.Calculated;
        CalculatedAt = now;
        RejectedAt = null;
        RejectedByUserId = null;
        RejectionReason = null;
    }

    /// <summary>ACD-11 (reject path): Department Head returns the batch to Faculty for re-entry. requirement-spec.md §8: "the ResultPublication state machine does not advance past Calculated/Verified until re-approved" - this does NOT change <see cref="Status"/> at all (stays `Calculated`); it only records rejection metadata, guarded by the identical `WHERE status = Calculated` conditional update as every other transition, so a concurrent Lock racing this Reject is still resolved correctly (see class remarks).</summary>
    public void Reject(string reason, Guid rejectedByUserId, DateTimeOffset now)
    {
        if (Status != ResultPublicationStatus.Calculated)
        {
            throw new InvalidOperationException($"Cannot reject a grade batch in status '{Status}' - only a 'Calculated' (submitted, not yet locked) batch can be rejected.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A rejection reason is mandatory.", nameof(reason));
        }

        RejectedAt = now;
        RejectedByUserId = rejectedByUserId;
        RejectionReason = reason.Trim();
    }

    /// <summary>
    /// ACD-11 (lock path): Department Head reviews and locks the batch. `Calculated` &#8594;
    /// `Verified`. Deliberately asserts its OWN required prior status directly (<see
    /// cref="ResultPublicationStatus.Calculated"/>) rather than going through a shared, dictionary-
    /// keyed-by-target-status helper: <see cref="ReenterForCorrection"/> ALSO targets `Verified`,
    /// from a different prior status (`Published`) - a generic "is `Verified` reachable from the
    /// current status" check cannot distinguish the two actions from each other and would
    /// incorrectly let either one apply from the other's source state.
    /// </summary>
    public void Lock(Guid lockedByUserId, DateTimeOffset now)
    {
        EnsureCurrentStatus(ResultPublicationStatus.Calculated, ResultPublicationStatus.Verified);
        Status = ResultPublicationStatus.Verified;
        LockedAt = now;
        LockedByUserId = lockedByUserId;
    }

    /// <summary>An authorized authority (Registrar/delegate) approves before publication. `Verified` &#8594; `Approved`.</summary>
    public void Approve(Guid approvedByUserId, DateTimeOffset now)
    {
        EnsureCurrentStatus(ResultPublicationStatus.Verified, ResultPublicationStatus.Approved);
        Status = ResultPublicationStatus.Approved;
        ApprovedAt = now;
        ApprovedByUserId = approvedByUserId;
    }

    /// <summary>ACD-12: `Approved` &#8594; `Published` - the underlying Grades become locked/visible to the Student (requirement-spec.md §2/§4).</summary>
    public void Publish(Guid publishedByUserId, DateTimeOffset now)
    {
        EnsureCurrentStatus(ResultPublicationStatus.Approved, ResultPublicationStatus.Published);
        Status = ResultPublicationStatus.Published;
        PublishedAt = now;
        PublishedByUserId = publishedByUserId;
    }

    /// <summary>`Published` &#8594; `Archived` - the state machine's terminal step.</summary>
    public void Archive(DateTimeOffset now)
    {
        EnsureCurrentStatus(ResultPublicationStatus.Published, ResultPublicationStatus.Archived);
        Status = ResultPublicationStatus.Archived;
        ArchivedAt = now;
    }

    /// <summary>ACD-13: the correction workflow's re-entry point (requirement-spec.md §2/§4/§9 decision 3) - `Published` &#8594; `Verified` ONLY, never a skip back to `Draft`/`Calculated`, and never a direct in-place edit of an already-published value. See <see cref="Lock"/>'s own remarks for why this asserts its prior status directly rather than through the shared target-status dictionary.</summary>
    public void ReenterForCorrection(DateTimeOffset now)
    {
        EnsureCurrentStatus(ResultPublicationStatus.Published, ResultPublicationStatus.Verified);
        Status = ResultPublicationStatus.Verified;
        CorrectionCount++;
        PublishedAt = null;
        PublishedByUserId = null;
    }

    private void EnsureCurrentStatus(ResultPublicationStatus expected, ResultPublicationStatus newStatus)
    {
        if (Status != expected)
        {
            throw new InvalidOperationException($"Cannot transition a ResultPublication from '{Status}' to '{newStatus}' - this is not a legal transition (requires '{expected}').");
        }
    }
}

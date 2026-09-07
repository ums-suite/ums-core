using UMS.Modules.Admission.Domain.Common;
using UMS.Modules.Admission.Domain.Events;
using UMS.Modules.Admission.Domain.MeritLists;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Admission.Domain.Results;

/// <summary>
/// ADM-17: one Campaign's published, per-Applicant admission outcome batch - requirement-spec.md
/// §2 Result Publication, reusing <c>Academic.ResultPublication</c>'s exact state-machine shape by
/// name (docs/ddd/ubiquitous-language.md's own note: "shares state-machine shape with Academic's
/// ResultPublication"). Modeled here as an aggregate root scoped per-Campaign, batching every
/// Applicant's own <see cref="AdmissionResultEntry"/> - a documented shape deviation from the
/// glossary's literal "Entity" categorization, made for the same reason
/// <c>Academic.ResultPublication</c> batches per-CourseOffering rather than per-Grade: the ADR-0007
/// write-through publish is fundamentally a BATCH operation (§5's up-to-100,000-applicant traffic
/// model), so the state machine that gates "is this batch safe to consider Published" has to live
/// at the batch's own aggregate boundary, not on each individual outcome.
///
/// <para>
/// <b>Concurrency mechanism - identical posture to <c>ResultPublication</c>.</b> Every transition
/// method below validates purely in-memory; the race-proof guarantee comes from
/// <c>AdmissionResultRepository.TryTransitionAsync</c>'s own state-guarded conditional
/// <c>UPDATE ... WHERE id = @id AND status = @expectedPriorStatus</c>, never from
/// <see cref="AggregateRoot{TId}.Version"/>.
/// </para>
/// </summary>
public sealed class AdmissionResult : AggregateRoot<AdmissionResultId>
{
    private readonly List<AdmissionResultEntry> _entries = [];

    private AdmissionResult()
    {
    }

    private AdmissionResult(AdmissionResultId id, Guid campaignId, Guid meritListId, DateTimeOffset now)
    {
        Id = id;
        CampaignId = campaignId;
        MeritListId = meritListId;
        Status = AdmissionResultStatus.Draft;
        CreatedAt = now;
    }

    public Guid CampaignId { get; private set; }

    public Guid MeritListId { get; private set; }

    public AdmissionResultStatus Status { get; private set; }

    public IReadOnlyCollection<AdmissionResultEntry> Entries => _entries.AsReadOnly();

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? CalculatedAt { get; private set; }

    public DateTimeOffset? LockedAt { get; private set; }

    public Guid? LockedByUserId { get; private set; }

    public DateTimeOffset? ApprovedAt { get; private set; }

    public Guid? ApprovedByUserId { get; private set; }

    public DateTimeOffset? PublishingStartedAt { get; private set; }

    public DateTimeOffset? PublishedAt { get; private set; }

    public Guid? PublishedByUserId { get; private set; }

    public DateTimeOffset? ArchivedAt { get; private set; }

    public int CorrectionCount { get; private set; }

    public static AdmissionResult Create(Guid campaignId, Guid meritListId, DateTimeOffset now) => new(AdmissionResultId.New(), campaignId, meritListId, now);

    /// <summary>requirement-spec.md §4: "AdmissionResult generation may only read from a MeritList in Approved state" - the caller's own precondition, checked before calling this (the MeritList itself is not referenced by id-only for a stale-read reason: cross-aggregate reads belong in the application service, never inside another aggregate).</summary>
    public Result Calculate(IReadOnlyCollection<MeritListEntry> meritListEntries, DateTimeOffset now)
    {
        if (Status != AdmissionResultStatus.Draft)
        {
            return Result.Failure(Error.Conflict("admission_result.not_draft", $"AdmissionResult '{Id}' is not Draft (currently '{Status}')."));
        }

        _entries.Clear();
        _entries.AddRange(meritListEntries.Select(e => new AdmissionResultEntry(e.ApplicantId, e.ApplicationId, e.ProgramId, e.Outcome, e.Rank, e.WaitlistRank)));
        Status = AdmissionResultStatus.Calculated;
        CalculatedAt = now;
        return Result.Success();
    }

    /// <summary>The Admission Officer's review/lock step. `Calculated` &#8594; `Verified`.</summary>
    public Result Lock(Guid lockedByUserId, DateTimeOffset now)
    {
        EnsureCurrentStatus(AdmissionResultStatus.Calculated, AdmissionResultStatus.Verified);
        Status = AdmissionResultStatus.Verified;
        LockedAt = now;
        LockedByUserId = lockedByUserId;
        return Result.Success();
    }

    /// <summary>The Registrar (or delegated authority)'s approval. `Verified` &#8594; `Approved`.</summary>
    public Result Approve(Guid approvedByUserId, DateTimeOffset now)
    {
        EnsureCurrentStatus(AdmissionResultStatus.Verified, AdmissionResultStatus.Approved);
        Status = AdmissionResultStatus.Approved;
        ApprovedAt = now;
        ApprovedByUserId = approvedByUserId;
        return Result.Success();
    }

    /// <summary>design-decisions.md's write-through-atomicity decision: `Approved` &#8594; `Publishing` - kicks off the PublishJob; the batch is NOT yet externally Published.</summary>
    public Result StartPublishing(DateTimeOffset now)
    {
        EnsureCurrentStatus(AdmissionResultStatus.Approved, AdmissionResultStatus.Publishing);
        Status = AdmissionResultStatus.Publishing;
        PublishingStartedAt = now;
        return Result.Success();
    }

    /// <summary>Called ONLY once the PublishJob reports 100% of the batch cache-verified (design-decisions.md) - `Publishing` &#8594; `Published`, the ADR-0007 trigger point.</summary>
    public Result MarkPublished(Guid publishedByUserId, DateTimeOffset now)
    {
        EnsureCurrentStatus(AdmissionResultStatus.Publishing, AdmissionResultStatus.Published);
        Status = AdmissionResultStatus.Published;
        PublishedAt = now;
        PublishedByUserId = publishedByUserId;
        Raise(new AdmissionResultPublished(Id.Value, CampaignId, _entries.Count, now));
        return Result.Success();
    }

    public Result Archive(DateTimeOffset now)
    {
        EnsureCurrentStatus(AdmissionResultStatus.Published, AdmissionResultStatus.Archived);
        Status = AdmissionResultStatus.Archived;
        ArchivedAt = now;
        return Result.Success();
    }

    /// <summary>requirement-spec.md §4/§9 decision 5: correction re-runs the SAME write-through generation state machine from `Verified` - `Published` &#8594; `Verified` only, never a skip back to `Draft`/`Calculated`.</summary>
    public Result ReenterForCorrection(DateTimeOffset now)
    {
        EnsureCurrentStatus(AdmissionResultStatus.Published, AdmissionResultStatus.Verified);
        Status = AdmissionResultStatus.Verified;
        CorrectionCount++;
        PublishedAt = null;
        PublishedByUserId = null;
        return Result.Success();
    }

    /// <summary>ADM-22: applied only once the caller (WaitlistPromotionService) has confirmed both the corresponding MeritListEntry promotion and the seat-quota headroom - this entry-level flip never re-validates the quota itself (that belongs to the MeritList/service, the single source of truth for ranking).</summary>
    public Result PromoteWaitlisted(Guid applicantId, Guid programId)
    {
        var entry = _entries.FirstOrDefault(e => e.ApplicantId == applicantId && e.ProgramId == programId);
        if (entry is null)
        {
            return Result.Failure(Error.NotFound("admission_result.entry_not_found", $"No AdmissionResultEntry exists for Applicant '{applicantId}' / Program '{programId}'."));
        }

        entry.ApplyPromotion();
        return Result.Success();
    }

    private void EnsureCurrentStatus(AdmissionResultStatus expected, AdmissionResultStatus newStatus)
    {
        if (Status != expected)
        {
            throw new InvalidOperationException($"Cannot transition an AdmissionResult from '{Status}' to '{newStatus}' - this is not a legal transition (requires '{expected}').");
        }
    }
}

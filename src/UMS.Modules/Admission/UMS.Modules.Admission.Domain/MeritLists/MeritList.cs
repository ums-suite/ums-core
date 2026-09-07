using UMS.Modules.Admission.Domain.Common;
using UMS.Modules.Admission.Domain.Events;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Admission.Domain.MeritLists;

/// <summary>
/// ADM-15/16: the ranked, reviewable, approvable output of evaluating all ExamAttempts for a
/// Campaign (docs/ddd/ubiquitous-language.md).
///
/// <para>
/// <b>Scope simplification, documented rather than silently narrowed.</b> requirement-spec.md §9
/// decision 1 already establishes one <c>Application</c> carries an ORDERED set of ProgramChoices
/// rather than fragmenting into one Application per choice; a fully general merit-list allocation
/// would then need to cascade a rejected top-choice down to an applicant's next preference across
/// every Program simultaneously (a stable-matching-style algorithm the BRD does not specify a
/// mechanism for). This build's <see cref="Generate"/> ranks each candidate against ONLY their
/// FIRST (<c>Rank == 1</c>) ProgramChoice - a first-pass engineering call in the same spirit as
/// §9's own numbered decisions, tracked here as a known gap for a follow-up multi-choice cascading
/// pass rather than silently assumed away.
/// </para>
/// </summary>
public sealed class MeritList : AggregateRoot<MeritListId>
{
    private readonly List<MeritListEntry> _entries = [];

    private MeritList()
    {
    }

    private MeritList(MeritListId id, Guid campaignId, DateTimeOffset now)
    {
        Id = id;
        CampaignId = campaignId;
        Status = MeritListStatus.Draft;
        GeneratedAt = now;
    }

    public Guid CampaignId { get; private set; }

    public MeritListStatus Status { get; private set; }

    public IReadOnlyCollection<MeritListEntry> Entries => _entries.AsReadOnly();

    public DateTimeOffset GeneratedAt { get; private set; }

    public DateTimeOffset? ApprovedAt { get; private set; }

    public Guid? ApprovedByUserId { get; private set; }

    /// <summary>
    /// ADM-15: one <paramref name="candidates"/> row per fully-`Evaluated` ExamAttempt whose
    /// Applicant satisfies at least one EligibilityRule for their first ProgramChoice (both checks
    /// performed by the calling service, which alone has access to the sibling Application/
    /// ExamAttempt/EligibilityRule data - edge-cases.md's "Merit-list generation running against a
    /// partially-evaluated set" is the caller's own precondition to enforce BEFORE calling this,
    /// never silently handled here). <paramref name="seatQuotas"/> is <c>(ProgramId, Quota)</c>
    /// (requirement-spec.md §4's seat-quota-bound invariant).
    /// </summary>
    public static Result<MeritList> Generate(Guid campaignId, IReadOnlyCollection<MeritCandidate> candidates, IReadOnlyDictionary<Guid, int> seatQuotas, DateTimeOffset now)
    {
        if (campaignId == Guid.Empty)
        {
            return Error.Validation("merit_list.campaign_id_required", "A MeritList requires a campaignId.");
        }

        var meritList = new MeritList(MeritListId.New(), campaignId, now);

        foreach (var group in candidates.GroupBy(c => c.ProgramId))
        {
            var quota = seatQuotas.GetValueOrDefault(group.Key, 0);
            var ranked = group.OrderByDescending(c => c.Score).ThenBy(c => c.ApplicantId).ToList();

            for (var i = 0; i < ranked.Count; i++)
            {
                var candidate = ranked[i];
                var rank = i + 1;
                var outcome = rank <= quota ? MeritOutcome.Admitted : MeritOutcome.Waitlisted;
                var waitlistRank = outcome == MeritOutcome.Waitlisted ? rank - quota : (int?)null;
                meritList._entries.Add(new MeritListEntry(candidate.ApplicantId, candidate.ApplicationId, candidate.ProgramId, candidate.Score, rank, outcome, waitlistRank));
            }
        }

        meritList.Raise(new MeritListGenerated(meritList.Id.Value, campaignId, meritList._entries.Count, now));
        return meritList;
    }

    /// <summary>ADM-16: Registrar (or delegated authority) approval, after Admission Officer review (requirement-spec.md §2).</summary>
    public Result Approve(Guid approvedByUserId, DateTimeOffset now)
    {
        if (Status != MeritListStatus.Draft)
        {
            return Result.Failure(Error.Conflict("merit_list.not_draft", $"MeritList '{Id}' is not Draft (currently '{Status}')."));
        }

        Status = MeritListStatus.Approved;
        ApprovedByUserId = approvedByUserId;
        ApprovedAt = now;
        Raise(new MeritListApproved(Id.Value, CampaignId, now));
        return Result.Success();
    }

    /// <summary>ADM-22: a higher-ranked waitlisted candidate promoted after an Admitted candidate for the same Program declines (requirement-spec.md §8/§9 decision 2) - manual, audited, never automatic. Rejected outright if the Program's quota has no room (the declining Admitted entry must be recorded as no-longer-occupying-a-seat by the caller first - see <c>WaitlistPromotionService</c>'s own remarks).</summary>
    public Result PromoteWaitlisted(Guid applicantId, Guid programId)
    {
        var entry = _entries.FirstOrDefault(e => e.ApplicantId == applicantId && e.ProgramId == programId);
        if (entry is null)
        {
            return Result.Failure(Error.NotFound("merit_list.entry_not_found", $"No MeritListEntry exists for Applicant '{applicantId}' / Program '{programId}'."));
        }

        if (entry.Outcome != MeritOutcome.Waitlisted)
        {
            return Result.Failure(Error.Conflict("merit_list.not_waitlisted", $"Applicant '{applicantId}''s entry for Program '{programId}' is '{entry.Outcome}', not Waitlisted."));
        }

        entry.SetOutcome(MeritOutcome.Admitted, waitlistRank: null);
        return Result.Success();
    }
}

/// <param name="Score">The ExamAttempt's own TotalScore, already fully Evaluated (both objective and subjective tracks) - the caller's own precondition, per this class's own <see cref="MeritList.Generate"/> remarks.</param>
public sealed record MeritCandidate(Guid ApplicantId, Guid ApplicationId, Guid ProgramId, decimal Score);

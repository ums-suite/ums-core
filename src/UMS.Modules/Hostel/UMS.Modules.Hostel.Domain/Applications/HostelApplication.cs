using UMS.Modules.Hostel.Domain.Common;
using UMS.Modules.Hostel.Domain.Events;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Hostel.Domain.Applications;

/// <summary>
/// HOS-3..6: a Student's request for accommodation (docs/ddd/ubiquitous-language.md), ranked and
/// reviewed before allocation. requirement-spec.md §3: <c>Draft -&gt; Submitted -&gt; Ranked -&gt;
/// Approved/Waitlisted/Rejected</c>, plus <see cref="HostelApplicationStatus.Withdrawn"/>
/// (design-decisions.md "HostelApplication Withdrawal as a First-Class State Transition").
///
/// <para>
/// <b>Withdrawal/approval concurrency mechanism - read this before changing <see cref="Withdraw"/>
/// or <see cref="Approve"/>.</b> edge-cases.md "HostelApplication withdrawn while its allocation is
/// mid-approval": both transitions are only ever called by
/// <c>HostelApplicationService</c> after it has taken a <c>SELECT ... FOR UPDATE</c> lock on this
/// same row (mirroring Finance's own <c>Payment</c>/<c>Invoice</c> lock pattern, and reused - not
/// duplicated - for the Bed-allocation race in <c>AllocationService</c>), so the two transitions
/// serialize rather than race. This class's own in-memory guards (<see cref="Withdraw"/> rejects
/// <see cref="HostelApplicationStatus.Approved"/>) are what actually resolve the race for whichever
/// caller wins the lock - the lock only supplies the serialization point.
/// </para>
///
/// <para>
/// <see cref="YearOfStudy"/>/<see cref="HasFinancialNeed"/>/<see cref="HomeDistrictDistanceKm"/> are
/// self-declared by the Student at submission time, never live-verified against Academic - mirroring
/// docs/ddd/ubiquitous-language.md's own precedent for Career's <c>EligibilityCriteria</c> ("all
/// optional, self-declared ... rather than live-verified against Academic"). Hostel has no outgoing
/// dependency on Academic (module-boundaries.md), and <c>IStudentStatusChecker</c> only exposes
/// Program/Department/Status, not year-of-study.
/// </para>
/// </summary>
public sealed class HostelApplication : AggregateRoot<HostelApplicationId>
{
    private readonly List<HostelPreference> _preferences = [];

    private HostelApplication()
    {
    }

    private HostelApplication(HostelApplicationId id, Guid studentId, Guid applicationWindowId, int yearOfStudy, bool hasFinancialNeed, decimal? homeDistrictDistanceKm, DateTimeOffset now)
    {
        Id = id;
        StudentId = studentId;
        ApplicationWindowId = applicationWindowId;
        YearOfStudy = yearOfStudy;
        HasFinancialNeed = hasFinancialNeed;
        HomeDistrictDistanceKm = homeDistrictDistanceKm;
        Status = HostelApplicationStatus.Draft;
        CreatedAt = now;
    }

    public Guid StudentId { get; private set; }

    public Guid ApplicationWindowId { get; private set; }

    public HostelApplicationStatus Status { get; private set; }

    public IReadOnlyCollection<HostelPreference> Preferences => _preferences.AsReadOnly();

    public int YearOfStudy { get; private set; }

    public bool HasFinancialNeed { get; private set; }

    public decimal? HomeDistrictDistanceKm { get; private set; }

    public decimal? EligibilityScore { get; private set; }

    public bool? IsEligible { get; private set; }

    public int? RankPosition { get; private set; }

    public string? DecisionReason { get; private set; }

    public Guid? AllocationId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? SubmittedAt { get; private set; }

    public DateTimeOffset? RankedAt { get; private set; }

    public DateTimeOffset? DecidedAt { get; private set; }

    public DateTimeOffset? WithdrawnAt { get; private set; }

    public static Result<HostelApplication> CreateDraft(Guid studentId, Guid applicationWindowId, int yearOfStudy, bool hasFinancialNeed, decimal? homeDistrictDistanceKm, DateTimeOffset now)
    {
        if (studentId == Guid.Empty || applicationWindowId == Guid.Empty)
        {
            return Error.Validation("hostel_application.identifiers_required", "A HostelApplication requires both a valid studentId and applicationWindowId.");
        }

        if (yearOfStudy <= 0)
        {
            return Error.Validation("hostel_application.year_of_study_invalid", "A HostelApplication's declared year of study must be a positive number.");
        }

        return new HostelApplication(HostelApplicationId.New(), studentId, applicationWindowId, yearOfStudy, hasFinancialNeed, homeDistrictDistanceKm, now);
    }

    public Result ReplacePreferences(IReadOnlyCollection<HostelPreference> preferences)
    {
        var mutability = EnsureDraft();
        if (mutability.IsFailure)
        {
            return mutability;
        }

        if (preferences is not { Count: > 0 })
        {
            return Result.Failure(Error.Validation("hostel_application.preferences_required", "At least one HostelPreference is required."));
        }

        if (preferences.Select(p => p.Rank).Distinct().Count() != preferences.Count)
        {
            return Result.Failure(Error.Validation("hostel_application.duplicate_preference_rank", "Each HostelPreference must have a distinct rank."));
        }

        _preferences.Clear();
        _preferences.AddRange(preferences.OrderBy(p => p.Rank));
        return Result.Success();
    }

    /// <summary>requirement-spec.md §2 step 2, §8 edge case "window closed -&gt; rejected with machine-readable error". The window-open/existing-active-allocation checks are cross-aggregate and performed by the calling service BEFORE this transition, under the same repository-level lock discipline described in this class's own remarks.</summary>
    public Result Submit(DateTimeOffset now)
    {
        if (Status != HostelApplicationStatus.Draft)
        {
            return Result.Failure(Error.Conflict("hostel_application.not_draft", $"HostelApplication '{Id}' is not Draft (currently '{Status}')."));
        }

        if (_preferences.Count == 0)
        {
            return Result.Failure(Error.Validation("hostel_application.no_preferences", "At least one HostelPreference is required before submitting."));
        }

        Status = HostelApplicationStatus.Submitted;
        SubmittedAt = now;
        Raise(new HostelApplicationSubmitted(Id.Value, StudentId, ApplicationWindowId, now));
        return Result.Success();
    }

    /// <summary>HOS-4: internal, drives the Officer review queue (requirement-spec.md §3). <paramref name="rankPosition"/> is null for an ineligible application (sorted last, never assigned a real queue position).</summary>
    public Result MarkRanked(decimal score, bool isEligible, int? rankPosition, DateTimeOffset now)
    {
        if (Status != HostelApplicationStatus.Submitted)
        {
            return Result.Failure(Error.Conflict("hostel_application.not_submitted", $"HostelApplication '{Id}' is not Submitted (currently '{Status}')."));
        }

        Status = HostelApplicationStatus.Ranked;
        EligibilityScore = score;
        IsEligible = isEligible;
        RankPosition = rankPosition;
        RankedAt = now;
        Raise(new HostelApplicationRanked(Id.Value, score, rankPosition ?? int.MaxValue, isEligible, now));
        return Result.Success();
    }

    /// <summary>requirement-spec.md §4: "A HostelApplication cannot reach Approved without a computed, passing eligibility result."</summary>
    public Result Approve(DateTimeOffset now)
    {
        if (Status is not (HostelApplicationStatus.Ranked or HostelApplicationStatus.Waitlisted))
        {
            return Result.Failure(Error.Conflict("hostel_application.not_reviewable", $"HostelApplication '{Id}' cannot be approved - it is currently '{Status}' (requires 'Ranked' or 'Waitlisted')."));
        }

        if (IsEligible != true)
        {
            return Result.Failure(Error.Conflict("hostel_application.not_eligible", $"HostelApplication '{Id}' has not passed eligibility and cannot be approved."));
        }

        Status = HostelApplicationStatus.Approved;
        DecidedAt = now;
        Raise(new HostelApplicationApproved(Id.Value, StudentId, now));
        return Result.Success();
    }

    public Result Waitlist(DateTimeOffset now)
    {
        if (Status != HostelApplicationStatus.Ranked)
        {
            return Result.Failure(Error.Conflict("hostel_application.not_ranked", $"HostelApplication '{Id}' cannot be waitlisted - it is currently '{Status}' (requires 'Ranked')."));
        }

        Status = HostelApplicationStatus.Waitlisted;
        DecidedAt = now;
        return Result.Success();
    }

    public Result Reject(string reason, DateTimeOffset now)
    {
        if (Status is not (HostelApplicationStatus.Ranked or HostelApplicationStatus.Waitlisted))
        {
            return Result.Failure(Error.Conflict("hostel_application.not_reviewable", $"HostelApplication '{Id}' cannot be rejected - it is currently '{Status}' (requires 'Ranked' or 'Waitlisted')."));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure(Error.Validation("hostel_application.rejection_reason_required", "A rejection requires a reason."));
        }

        Status = HostelApplicationStatus.Rejected;
        DecisionReason = reason.Trim();
        DecidedAt = now;
        Raise(new HostelApplicationRejected(Id.Value, StudentId, DecisionReason, now));
        return Result.Success();
    }

    /// <summary>edge-cases.md "HostelApplication withdrawn while its allocation is mid-approval" - reachable from every non-terminal, non-Approved state (requirement-spec.md §2 Check-out already covers "Student no longer wants this bed" once Approved).</summary>
    public Result Withdraw(DateTimeOffset now)
    {
        if (Status is not (HostelApplicationStatus.Draft or HostelApplicationStatus.Submitted or HostelApplicationStatus.Ranked or HostelApplicationStatus.Waitlisted))
        {
            return Result.Failure(Error.Conflict(
                "hostel_application.not_withdrawable",
                $"HostelApplication '{Id}' cannot be withdrawn - it is currently '{Status}'. Once Approved, use the Allocation check-out flow instead."));
        }

        Status = HostelApplicationStatus.Withdrawn;
        WithdrawnAt = now;
        Raise(new HostelApplicationWithdrawn(Id.Value, StudentId, now));
        return Result.Success();
    }

    /// <summary>HOS-7: recorded once the Bed-allocation command has committed a new Allocation for this Application (requirement-spec.md §2 step 6).</summary>
    public void RecordAllocation(Guid allocationId) => AllocationId = allocationId;

    private Result EnsureDraft() =>
        Status == HostelApplicationStatus.Draft
            ? Result.Success()
            : Result.Failure(Error.Conflict("hostel_application.not_draft", $"HostelApplication '{Id}' is '{Status}' and can no longer be edited."));
}

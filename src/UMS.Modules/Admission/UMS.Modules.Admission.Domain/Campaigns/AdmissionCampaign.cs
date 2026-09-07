using UMS.Modules.Admission.Domain.Common;
using UMS.Modules.Admission.Domain.Events;
using UMS.Shared.Domain;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Admission.Domain.Campaigns;

/// <summary>
/// ADM-1: a time-boxed admission cycle for one or more Programs (docs/ddd/ubiquitous-language.md).
///
/// <para>
/// <b>Configuration immutability (requirement-spec.md §2/§9 decision 6).</b> "A campaign is
/// immutable on its eligibility/fee shape once the first Application is submitted against it -
/// later edits apply only to future campaigns, never retroactively to in-flight applications."
/// <see cref="IsConfigurationLocked"/> flips exactly once, from <see cref="ApplicationService"/>'s
/// own first-successful-submit branch calling <see cref="LockConfiguration"/> - the campaign itself
/// becomes the version boundary (§9 decision 6's own stated resolution), so an already-`Submitted`/
/// `Locked` Application is evaluated against whatever rule version was in force at ITS OWN submit
/// time (edge-cases.md, "EligibilityRule changed mid-cycle") simply because a locked campaign can no
/// longer change under it.
/// </para>
/// </summary>
public sealed class AdmissionCampaign : AggregateRoot<AdmissionCampaignId>
{
    private readonly List<Guid> _programIds = [];
    private readonly List<EligibilityRule> _eligibilityRules = [];
    private readonly List<SeatQuota> _seatQuotas = [];
    private readonly List<string> _requiredDocumentTypes = [];

    private AdmissionCampaign()
    {
    }

    private AdmissionCampaign(
        AdmissionCampaignId id,
        string name,
        IReadOnlyCollection<Guid> programIds,
        DateRange applicationWindow,
        string applicationFeeType,
        string confirmationFeeType,
        DateTimeOffset now)
    {
        Id = id;
        Name = name;
        _programIds.AddRange(programIds);
        ApplicationWindow = applicationWindow;
        ApplicationFeeType = applicationFeeType;
        ConfirmationFeeType = confirmationFeeType;
        IsConfigurationLocked = false;
        CreatedAt = now;
    }

    public string Name { get; private set; } = string.Empty;

    public IReadOnlyCollection<Guid> ProgramIds => _programIds.AsReadOnly();

    public DateRange ApplicationWindow { get; private set; } = null!;

    /// <summary>requirement-spec.md §9 decision 3: the application fee and the confirmation fee are two distinct Finance Invoices, never one fee split across two states.</summary>
    public string ApplicationFeeType { get; private set; } = string.Empty;

    public string ConfirmationFeeType { get; private set; } = string.Empty;

    public IReadOnlyCollection<EligibilityRule> EligibilityRules => _eligibilityRules.AsReadOnly();

    public IReadOnlyCollection<SeatQuota> SeatQuotas => _seatQuotas.AsReadOnly();

    /// <summary>requirement-spec.md §2 Draft: "uploads required ApplicationDocuments" - the campaign-defined checklist ADM-8's submit gate checks for presence against (requirement-spec.md §2 Submit gate).</summary>
    public IReadOnlyCollection<string> RequiredDocumentTypes => _requiredDocumentTypes.AsReadOnly();

    public bool IsConfigurationLocked { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static Result<AdmissionCampaign> Create(
        string name,
        IReadOnlyCollection<Guid> programIds,
        DateRange applicationWindow,
        string applicationFeeType,
        string confirmationFeeType,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Error.Validation("campaign.name_required", "An AdmissionCampaign's name is required.");
        }

        if (programIds is not { Count: > 0 })
        {
            return Error.Validation("campaign.programs_required", "An AdmissionCampaign requires at least one Program.");
        }

        ArgumentNullException.ThrowIfNull(applicationWindow);

        if (string.IsNullOrWhiteSpace(applicationFeeType) || string.IsNullOrWhiteSpace(confirmationFeeType))
        {
            return Error.Validation("campaign.fee_types_required", "An AdmissionCampaign's application and confirmation fee types are both required.");
        }

        var campaign = new AdmissionCampaign(AdmissionCampaignId.New(), name.Trim(), programIds.Distinct().ToList(), applicationWindow, applicationFeeType.Trim(), confirmationFeeType.Trim(), now);
        campaign.Raise(new AdmissionCampaignCreated(campaign.Id.Value, campaign.Name, now));
        return campaign;
    }

    /// <summary>ADM-1: rejected once <see cref="IsConfigurationLocked"/> (requirement-spec.md §2) - editing dates/fee shape/rules never applies retroactively.</summary>
    public Result AddEligibilityRule(EligibilityRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        var mutability = EnsureMutable();
        if (mutability.IsFailure)
        {
            return mutability;
        }

        if (!_programIds.Contains(rule.ProgramId))
        {
            return Result.Failure(Error.Validation("campaign.rule_program_not_in_campaign", $"Program '{rule.ProgramId}' is not one of this campaign's own Programs."));
        }

        _eligibilityRules.Add(rule);
        return Result.Success();
    }

    public Result AddSeatQuota(SeatQuota quota)
    {
        ArgumentNullException.ThrowIfNull(quota);

        var mutability = EnsureMutable();
        if (mutability.IsFailure)
        {
            return mutability;
        }

        if (!_programIds.Contains(quota.ProgramId))
        {
            return Result.Failure(Error.Validation("campaign.quota_program_not_in_campaign", $"Program '{quota.ProgramId}' is not one of this campaign's own Programs."));
        }

        if (_seatQuotas.Any(q => q.ProgramId == quota.ProgramId))
        {
            return Result.Failure(Error.Conflict("campaign.quota_already_set", $"A SeatQuota already exists for Program '{quota.ProgramId}'."));
        }

        _seatQuotas.Add(quota);
        return Result.Success();
    }

    public Result AddRequiredDocumentType(string documentType)
    {
        if (string.IsNullOrWhiteSpace(documentType))
        {
            return Result.Failure(Error.Validation("campaign.document_type_required", "A required document type name cannot be blank."));
        }

        var mutability = EnsureMutable();
        if (mutability.IsFailure)
        {
            return mutability;
        }

        var trimmed = documentType.Trim();
        if (!_requiredDocumentTypes.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
        {
            _requiredDocumentTypes.Add(trimmed);
        }

        return Result.Success();
    }

    /// <summary>Called exactly once, from <c>ApplicationService</c>'s own first-successful-submit branch - idempotent thereafter (a second call is a silent no-op, never an error), since a retried/duplicate submit against an already-Locked Application must never fail on THIS side effect.</summary>
    public void LockConfiguration()
    {
        IsConfigurationLocked = true;
    }

    public Result EnsureMutable() =>
        IsConfigurationLocked
            ? Result.Failure(Error.Conflict("campaign.configuration_locked", $"AdmissionCampaign '{Id}' is locked - its eligibility/fee shape cannot change once the first Application has been submitted against it."))
            : Result.Success();

    public EligibilityRule? GetEligibilityRule(Guid programId) => _eligibilityRules.FirstOrDefault(r => r.ProgramId == programId);

    public SeatQuota? GetSeatQuota(Guid programId) => _seatQuotas.FirstOrDefault(q => q.ProgramId == programId);
}

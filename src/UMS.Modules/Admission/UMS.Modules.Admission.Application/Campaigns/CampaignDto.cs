namespace UMS.Modules.Admission.Application.Campaigns;

public sealed record CampaignDto(
    Guid Id,
    string Name,
    IReadOnlyCollection<Guid> ProgramIds,
    DateOnly ApplicationWindowStart,
    DateOnly ApplicationWindowEnd,
    string ApplicationFeeType,
    string ConfirmationFeeType,
    bool IsConfigurationLocked,
    IReadOnlyCollection<string> RequiredDocumentTypes);

public sealed record CreateCampaignRequest(
    string Name,
    IReadOnlyCollection<Guid> ProgramIds,
    DateOnly ApplicationWindowStart,
    DateOnly ApplicationWindowEnd,
    string ApplicationFeeType,
    string ConfirmationFeeType);

public sealed record EligibilityRuleRequest(Guid ProgramId, decimal MinimumScore, bool IsGpaScale, string? RequiredBoard);

public sealed record SeatQuotaRequest(Guid ProgramId, int Quota);

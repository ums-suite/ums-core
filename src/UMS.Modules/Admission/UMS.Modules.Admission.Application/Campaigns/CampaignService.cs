using UMS.Modules.Admission.Application.Abstractions;
using UMS.Modules.Admission.Domain.Campaigns;
using UMS.Shared.Domain;
using UMS.Shared.ErrorHandling.Results;
using UMS.Shared.Organization;

namespace UMS.Modules.Admission.Application.Campaigns;

/// <summary>ADM-1: Campaign Setup (requirement-spec.md §2, §6 <c>POST /campaigns</c>).</summary>
public sealed class CampaignService(
    ICampaignRepository campaigns,
    IOrganizationNodeExistenceChecker organizationNodes,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<Result<CampaignDto>> CreateAsync(CreateCampaignRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        foreach (var programId in request.ProgramIds ?? [])
        {
            if (!await organizationNodes.ExistsAsync(programId, cancellationToken).ConfigureAwait(false))
            {
                return Error.Validation("campaign.program_not_found", $"No Program exists with id '{programId}'.");
            }
        }

        var window = DateRange.Create(request.ApplicationWindowStart, request.ApplicationWindowEnd);
        if (window.IsFailure)
        {
            return window.Error!;
        }

        var created = AdmissionCampaign.Create(request.Name, request.ProgramIds ?? [], window.Value, request.ApplicationFeeType, request.ConfirmationFeeType, clock.UtcNow);
        if (created.IsFailure)
        {
            return created.Error!;
        }

        campaigns.Add(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(created.Value);
    }

    public async Task<Result<CampaignDto>> AddEligibilityRuleAsync(Guid campaignId, EligibilityRuleRequest request, CancellationToken cancellationToken = default)
    {
        var campaign = await campaigns.GetByIdAsync(new AdmissionCampaignId(campaignId), cancellationToken).ConfigureAwait(false);
        if (campaign is null)
        {
            return Error.NotFound("campaign.not_found", $"No AdmissionCampaign exists with id '{campaignId}'.");
        }

        var score = request.IsGpaScale ? PercentageOrGpa.CreateGpa(request.MinimumScore) : PercentageOrGpa.CreatePercentage(request.MinimumScore);
        if (score.IsFailure)
        {
            return score.Error!;
        }

        var rule = EligibilityRule.Create(request.ProgramId, score.Value, request.RequiredBoard);
        if (rule.IsFailure)
        {
            return rule.Error!;
        }

        var added = campaign.AddEligibilityRule(rule.Value);
        if (added.IsFailure)
        {
            return added.Error!;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(campaign);
    }

    public async Task<Result<CampaignDto>> AddSeatQuotaAsync(Guid campaignId, SeatQuotaRequest request, CancellationToken cancellationToken = default)
    {
        var campaign = await campaigns.GetByIdAsync(new AdmissionCampaignId(campaignId), cancellationToken).ConfigureAwait(false);
        if (campaign is null)
        {
            return Error.NotFound("campaign.not_found", $"No AdmissionCampaign exists with id '{campaignId}'.");
        }

        var quota = SeatQuota.Create(request.ProgramId, request.Quota);
        if (quota.IsFailure)
        {
            return quota.Error!;
        }

        var added = campaign.AddSeatQuota(quota.Value);
        if (added.IsFailure)
        {
            return added.Error!;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(campaign);
    }

    public async Task<Result<CampaignDto>> AddRequiredDocumentTypeAsync(Guid campaignId, string documentType, CancellationToken cancellationToken = default)
    {
        var campaign = await campaigns.GetByIdAsync(new AdmissionCampaignId(campaignId), cancellationToken).ConfigureAwait(false);
        if (campaign is null)
        {
            return Error.NotFound("campaign.not_found", $"No AdmissionCampaign exists with id '{campaignId}'.");
        }

        var added = campaign.AddRequiredDocumentType(documentType);
        if (added.IsFailure)
        {
            return added.Error!;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(campaign);
    }

    public async Task<Result<CampaignDto>> GetByIdAsync(Guid campaignId, CancellationToken cancellationToken = default)
    {
        var campaign = await campaigns.GetByIdAsync(new AdmissionCampaignId(campaignId), cancellationToken).ConfigureAwait(false);
        return campaign is null
            ? Error.NotFound("campaign.not_found", $"No AdmissionCampaign exists with id '{campaignId}'.")
            : ToDto(campaign);
    }

    internal static CampaignDto ToDto(AdmissionCampaign campaign) => new(
        campaign.Id.Value,
        campaign.Name,
        campaign.ProgramIds,
        campaign.ApplicationWindow.Start,
        campaign.ApplicationWindow.End,
        campaign.ApplicationFeeType,
        campaign.ConfirmationFeeType,
        campaign.IsConfigurationLocked,
        campaign.RequiredDocumentTypes);
}

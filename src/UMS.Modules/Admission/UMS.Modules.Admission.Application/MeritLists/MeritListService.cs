using UMS.Modules.Admission.Application.Abstractions;
using UMS.Modules.Admission.Application.Common;
using UMS.Modules.Admission.Domain.Applications;
using UMS.Modules.Admission.Domain.Campaigns;
using UMS.Modules.Admission.Domain.ExamAttempts;
using UMS.Modules.Admission.Domain.MeritLists;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Admission.Application.MeritLists;

/// <summary>ADM-15/16: Merit List Generation, Review &amp; Approval (requirement-spec.md §2).</summary>
public sealed class MeritListService(
    IMeritListRepository meritLists,
    ICampaignRepository campaigns,
    IApplicationRepository applications,
    IApplicantRepository applicants,
    IExamAttemptRepository attempts,
    IAdmissionTestRepository tests,
    IUnitOfWork unitOfWork,
    IAuditRecorder auditRecorder,
    IClock clock)
{
    /// <summary>edge-cases.md "Merit-list generation running against a partially-evaluated set of ExamAttempts": blocks entirely (never a partial/provisional list) until every in-scope ExamAttempt is fully Evaluated.</summary>
    public async Task<Result<MeritListDto>> GenerateAsync(Guid campaignId, string correlationId, CancellationToken cancellationToken = default)
    {
        var campaign = await campaigns.GetByIdAsync(new AdmissionCampaignId(campaignId), cancellationToken).ConfigureAwait(false);
        if (campaign is null)
        {
            return Error.NotFound("merit_list.campaign_not_found", $"No AdmissionCampaign exists with id '{campaignId}'.");
        }

        var test = await tests.GetByCampaignIdAsync(campaignId, cancellationToken).ConfigureAwait(false);
        if (test is null)
        {
            return Error.NotFound("merit_list.test_not_found", $"No AdmissionTest exists for Campaign '{campaignId}'.");
        }

        var lockedApplications = await applications.GetLockedByCampaignAsync(campaignId, cancellationToken).ConfigureAwait(false);
        var candidates = new List<MeritCandidate>();
        var pendingCount = 0;

        foreach (var application in lockedApplications)
        {
            var attempt = await attempts.GetByApplicantAndTestAsync(application.ApplicantId, test.Id.Value, cancellationToken).ConfigureAwait(false);
            if (attempt is null || attempt.Status != ExamAttemptStatus.Submitted)
            {
                continue;
            }

            if (attempt.EvaluationStatus != EvaluationStatus.Evaluated)
            {
                pendingCount++;
                continue;
            }

            var firstChoice = application.ProgramChoices.OrderBy(c => c.Rank).FirstOrDefault();
            if (firstChoice is null)
            {
                continue;
            }

            var rule = campaign.GetEligibilityRule(firstChoice.ProgramId);
            if (rule is not null)
            {
                var applicant = await applicants.GetByIdAsync(new Domain.Applicants.ApplicantId(application.ApplicantId), cancellationToken).ConfigureAwait(false);
                if (applicant is null || !applicant.AcademicHistory.Any(rule.IsSatisfiedBy))
                {
                    continue;
                }
            }

            candidates.Add(new MeritCandidate(application.ApplicantId, application.Id.Value, firstChoice.ProgramId, attempt.TotalScore ?? 0));
        }

        if (pendingCount > 0)
        {
            return Error.Conflict("merit_list.evaluation_incomplete", $"{pendingCount} ExamAttempt(s) are still pending evaluation - MeritList generation is blocked until every attempt is Evaluated.");
        }

        var quotas = campaign.SeatQuotas.ToDictionary(q => q.ProgramId, q => q.Quota);
        var generated = MeritList.Generate(campaignId, candidates, quotas, clock.UtcNow);
        if (generated.IsFailure)
        {
            return generated.Error!;
        }

        meritLists.Add(generated.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(generated.Value);
    }

    public async Task<Result<MeritListDto>> ApproveAsync(Guid meritListId, Guid approvedByUserId, string correlationId, CancellationToken cancellationToken = default)
    {
        var meritList = await meritLists.GetByIdAsync(new MeritListId(meritListId), cancellationToken).ConfigureAwait(false);
        if (meritList is null)
        {
            return Error.NotFound("merit_list.not_found", $"No MeritList exists with id '{meritListId}'.");
        }

        var approved = meritList.Approve(approvedByUserId, clock.UtcNow);
        if (approved.IsFailure)
        {
            return approved.Error!;
        }

        var auditRequest = new AuditContext(approvedByUserId, ActorIpAddress: null, correlationId)
            .ToRequest("MeritList", meritList.Id.Value.ToString(), AuditActions.Approve, "{\"status\":\"Draft\"}", "{\"status\":\"Approved\"}");
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var committed = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        return committed.IsFailure ? committed.Error! : ToDto(meritList);
    }

    public async Task<Result<MeritListDto>> GetByCampaignIdAsync(Guid campaignId, CancellationToken cancellationToken = default)
    {
        var meritList = await meritLists.GetByCampaignIdAsync(campaignId, cancellationToken).ConfigureAwait(false);
        return meritList is null
            ? Error.NotFound("merit_list.not_found", $"No MeritList exists for Campaign '{campaignId}'.")
            : ToDto(meritList);
    }

    internal static MeritListDto ToDto(MeritList meritList) => new(
        meritList.Id.Value,
        meritList.CampaignId,
        meritList.Status.ToString(),
        meritList.Entries.Select(e => new MeritListEntryDto(e.ApplicantId, e.ApplicationId, e.ProgramId, e.Score, e.Rank, e.Outcome.ToString(), e.WaitlistRank)).ToList());
}

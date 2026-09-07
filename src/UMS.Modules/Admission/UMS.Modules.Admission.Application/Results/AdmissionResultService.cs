using UMS.Modules.Admission.Application.Abstractions;
using UMS.Modules.Admission.Application.Common;
using UMS.Modules.Admission.Domain.MeritLists;
using UMS.Modules.Admission.Domain.Publishing;
using UMS.Modules.Admission.Domain.Results;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Admission.Application.Results;

/// <summary>ADM-17: Result Publication (requirement-spec.md §2). The literal single `POST /results/{campaignId}/publish` endpoint requirement-spec.md §6 names is split into calculate/lock/approve/publish sub-steps, the same documented deviation Academic's/Faculty's own endpoint splits already established (requirement-spec.md's own state-machine shape demands a review→approve gate before publish is ever reachable).</summary>
public sealed class AdmissionResultService(
    IAdmissionResultRepository results,
    IMeritListRepository meritLists,
    IPublishJobRepository publishJobs,
    IUnitOfWork unitOfWork,
    IAuditRecorder auditRecorder,
    IClock clock)
{
    /// <summary>requirement-spec.md §4: "AdmissionResult generation may only read from a MeritList in Approved state."</summary>
    public async Task<Result<AdmissionResultDto>> CalculateAsync(Guid campaignId, CancellationToken cancellationToken = default)
    {
        var meritList = await meritLists.GetByCampaignIdAsync(campaignId, cancellationToken).ConfigureAwait(false);
        if (meritList is null)
        {
            return Error.NotFound("admission_result.merit_list_not_found", $"No MeritList exists for Campaign '{campaignId}'.");
        }

        if (meritList.Status != MeritListStatus.Approved)
        {
            return Error.Conflict("admission_result.merit_list_not_approved", $"MeritList '{meritList.Id}' is not Approved (currently '{meritList.Status}') - AdmissionResult cannot be calculated from it.");
        }

        var result = AdmissionResult.Create(campaignId, meritList.Id.Value, clock.UtcNow);
        var calculated = result.Calculate(meritList.Entries, clock.UtcNow);
        if (calculated.IsFailure)
        {
            return calculated.Error!;
        }

        results.Add(result);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(result);
    }

    public async Task<Result<AdmissionResultDto>> LockAsync(Guid admissionResultId, Guid lockedByUserId, string correlationId, CancellationToken cancellationToken = default) =>
        await TransitionAsync(admissionResultId, r => r.Lock(lockedByUserId, clock.UtcNow), lockedByUserId, correlationId, AuditActions.Update, cancellationToken).ConfigureAwait(false);

    public async Task<Result<AdmissionResultDto>> ApproveAsync(Guid admissionResultId, Guid approvedByUserId, string correlationId, CancellationToken cancellationToken = default) =>
        await TransitionAsync(admissionResultId, r => r.Approve(approvedByUserId, clock.UtcNow), approvedByUserId, correlationId, AuditActions.Approve, cancellationToken).ConfigureAwait(false);

    /// <summary>Kicks off publication - the batch is NOT yet externally Published; <c>PublishJobRelayWorker</c> completes it once every applicant's cache keys are verified (design-decisions.md). Creates the <see cref="PublishJob"/> tracking entity in the SAME transaction as the Approved-to-Publishing transition.</summary>
    public async Task<Result<AdmissionResultDto>> StartPublishingAsync(Guid admissionResultId, Guid actingUserId, string correlationId, CancellationToken cancellationToken = default)
    {
        var result = await results.GetByIdAsync(new AdmissionResultId(admissionResultId), cancellationToken).ConfigureAwait(false);
        if (result is null)
        {
            return Error.NotFound("admission_result.not_found", $"No AdmissionResult exists with id '{admissionResultId}'.");
        }

        var beforeStatus = result.Status.ToString();
        var transitioned = result.StartPublishing(clock.UtcNow);
        if (transitioned.IsFailure)
        {
            return transitioned.Error!;
        }

        var job = PublishJob.Create(result.Id.Value, result.CampaignId, result.Entries.Count, clock.UtcNow);
        if (job.IsFailure)
        {
            return job.Error!;
        }

        publishJobs.Add(job.Value);

        var auditRequest = new AuditContext(actingUserId, ActorIpAddress: null, correlationId)
            .ToRequest("AdmissionResult", result.Id.Value.ToString(), AuditActions.Publish, $"{{\"status\":\"{beforeStatus}\"}}", $"{{\"status\":\"{result.Status}\",\"publishJobId\":\"{job.Value.Id}\"}}");
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var committed = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        return committed.IsFailure ? committed.Error! : ToDto(result);
    }

    public async Task<Result<AdmissionResultDto>> ReenterForCorrectionAsync(Guid admissionResultId, Guid actingUserId, string correlationId, CancellationToken cancellationToken = default) =>
        await TransitionAsync(admissionResultId, r => r.ReenterForCorrection(clock.UtcNow), actingUserId, correlationId, AuditActions.Update, cancellationToken).ConfigureAwait(false);

    public async Task<Result<AdmissionResultDto>> GetByCampaignIdAsync(Guid campaignId, CancellationToken cancellationToken = default)
    {
        var result = await results.GetByCampaignIdAsync(campaignId, cancellationToken).ConfigureAwait(false);
        return result is null
            ? Error.NotFound("admission_result.not_found", $"No AdmissionResult exists for Campaign '{campaignId}'.")
            : ToDto(result);
    }

    internal static AdmissionResultDto ToDto(AdmissionResult result) => new(
        result.Id.Value,
        result.CampaignId,
        result.MeritListId,
        result.Status.ToString(),
        result.Entries.Select(e => new AdmissionResultEntryDto(e.ApplicantId, e.ApplicationId, e.ProgramId, e.Outcome.ToString(), e.MeritRank, e.WaitlistRank)).ToList());

    private async Task<Result<AdmissionResultDto>> TransitionAsync(Guid admissionResultId, Func<AdmissionResult, Result> transition, Guid actingUserId, string correlationId, string auditAction, CancellationToken cancellationToken)
    {
        var result = await results.GetByIdAsync(new AdmissionResultId(admissionResultId), cancellationToken).ConfigureAwait(false);
        if (result is null)
        {
            return Error.NotFound("admission_result.not_found", $"No AdmissionResult exists with id '{admissionResultId}'.");
        }

        var beforeStatus = result.Status.ToString();
        var transitioned = transition(result);
        if (transitioned.IsFailure)
        {
            return transitioned.Error!;
        }

        var applicationsAffected = result.Entries.Count;
        var auditRequest = new AuditContext(actingUserId, ActorIpAddress: null, correlationId)
            .ToRequest("AdmissionResult", result.Id.Value.ToString(), auditAction, $"{{\"status\":\"{beforeStatus}\"}}", $"{{\"status\":\"{result.Status}\",\"entries\":{applicationsAffected}}}");
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var committed = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        return committed.IsFailure ? committed.Error! : ToDto(result);
    }
}

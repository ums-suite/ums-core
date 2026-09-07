using UMS.Modules.Admission.Application.Abstractions;
using UMS.Modules.Admission.Application.Common;
using UMS.Modules.Admission.Domain.MeritLists;
using UMS.Modules.Admission.Domain.Results;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Admission.Application.Results;

/// <summary>ADM-22: waitlist promotion - a manual, audited admin-triggered action, never an automatic real-time reshuffle (requirement-spec.md §8 edge case, §9 decision 2). Updates the MeritList (the ranking source of truth) and the published AdmissionResult together so a later correction re-publish is consistent with the promotion already granted.</summary>
public sealed class WaitlistPromotionService(
    IMeritListRepository meritLists,
    IAdmissionResultRepository results,
    IUnitOfWork unitOfWork,
    IAuditRecorder auditRecorder)
{
    public async Task<Result> PromoteAsync(Guid campaignId, Guid applicantId, Guid programId, Guid actingUserId, string correlationId, CancellationToken cancellationToken = default)
    {
        var meritList = await meritLists.GetByCampaignIdAsync(campaignId, cancellationToken).ConfigureAwait(false);
        if (meritList is null)
        {
            return Result.Failure(Error.NotFound("waitlist.merit_list_not_found", $"No MeritList exists for Campaign '{campaignId}'."));
        }

        var promoted = meritList.PromoteWaitlisted(applicantId, programId);
        if (promoted.IsFailure)
        {
            return promoted;
        }

        var result = await results.GetByCampaignIdAsync(campaignId, cancellationToken).ConfigureAwait(false);
        result?.PromoteWaitlisted(applicantId, programId);

        var auditRequest = new AuditContext(actingUserId, ActorIpAddress: null, correlationId)
            .ToRequest("MeritList", meritList.Id.Value.ToString(), "promote_waitlisted", null, $"{{\"applicantId\":\"{applicantId}\",\"programId\":\"{programId}\"}}");
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        return await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
    }
}

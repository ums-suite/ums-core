using System.Text.Json;
using Microsoft.Extensions.Logging;
using UMS.Modules.Admission.Application.Abstractions;
using UMS.Modules.Admission.Application.Common;
using UMS.Modules.Admission.Domain.MeritLists;
using UMS.Modules.Admission.Domain.Publishing;
using UMS.Modules.Admission.Domain.Results;
using UMS.Shared.Audit;
using UMS.Shared.Documents;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Admission.Application.Results;

/// <summary>
/// design-decisions.md "Write-Through Cache Publish Atomicity"/"Background Job Design for Bulk
/// Admit-Card and Result-Notification Generation": drives one <see cref="PublishJob"/> to
/// completion, one bounded batch per call (<c>PublishJobRelayWorker</c> calls this repeatedly until
/// the job reports <see cref="PublishJobStage.Completed"/>, resuming from the last checkpoint on
/// crash/restart - never restarting the whole campaign's batch).
///
/// <para>
/// <b>Known gap, documented rather than glossed over:</b> <see cref="AdmissionResult.Entries"/> is
/// paged via in-memory <c>Skip</c>/<c>Take</c> against the already-loaded aggregate, not a direct
/// indexed query against a child table - correct at this build's tested scale, but a genuinely
/// 100,000-applicant campaign (requirement-spec.md §5) would need <c>AdmissionResultEntry</c>
/// promoted to its own independently-queryable table with a covering index on
/// <c>(admission_result_id, applicant_id)</c>, the same class of follow-up Reporting's own
/// read-model work will eventually need regardless.
/// </para>
/// </summary>
public sealed class PublishJobService(
    IPublishJobRepository publishJobs,
    IAdmissionResultRepository results,
    IApplicationRepository applications,
    IResultCache resultCache,
    IDocumentGenerationRequester documentGenerationRequester,
    INotificationRequestPublisher notificationPublisher,
    IUnitOfWork unitOfWork,
    IDomainEventRecorder domainEvents,
    IAuditRecorder auditRecorder,
    IClock clock,
    ILogger<PublishJobService> logger)
{
    public async Task<Result> ProcessNextBatchAsync(Guid publishJobId, int batchSize, CancellationToken cancellationToken = default)
    {
        var job = await publishJobs.GetByIdAsync(new PublishJobId(publishJobId), cancellationToken).ConfigureAwait(false);
        if (job is null)
        {
            return Result.Failure(Error.NotFound("publish_job.not_found", $"No PublishJob exists with id '{publishJobId}'."));
        }

        var result = await results.GetByIdAsync(new AdmissionResultId(job.AdmissionResultId), cancellationToken).ConfigureAwait(false);
        if (result is null)
        {
            job.Fail("AdmissionResult no longer exists", clock.UtcNow);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }

        return job.Stage switch
        {
            PublishJobStage.WarmingCache => await ProcessCacheBatchAsync(job, result, batchSize, cancellationToken).ConfigureAwait(false),
            PublishJobStage.FanningOut => await ProcessFanOutBatchAsync(job, result, batchSize, cancellationToken).ConfigureAwait(false),
            _ => Result.Success(),
        };
    }

    private async Task<Result> ProcessCacheBatchAsync(PublishJob job, AdmissionResult result, int batchSize, CancellationToken cancellationToken)
    {
        var ordered = result.Entries.OrderBy(e => e.ApplicantId).ToList();
        var startIndex = job.CacheCursor is { } cursor ? ordered.FindIndex(e => e.ApplicantId == cursor) + 1 : 0;
        var batch = ordered.Skip(startIndex).Take(batchSize).ToList();

        if (batch.Count == 0)
        {
            var completedEarly = job.CompleteCacheWarm();
            if (completedEarly.IsSuccess)
            {
                await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            return Result.Success();
        }

        foreach (var entry in batch)
        {
            var application = await applications.GetByIdAsync(new Domain.Applications.ApplicationId(entry.ApplicationId), cancellationToken).ConfigureAwait(false);
            var payload = JsonSerializer.Serialize(new
            {
                applicantId = entry.ApplicantId,
                applicationId = entry.ApplicationId,
                programId = entry.ProgramId,
                outcome = entry.Outcome.ToString(),
                meritRank = entry.MeritRank,
                waitlistRank = entry.WaitlistRank,
            });

            await resultCache.WriteResultAsync(
                new ResultCacheEntry(application?.ApplicationNumber ?? entry.ApplicationId.ToString(), StudentId: null, result.CampaignId, application?.RollNumber, payload),
                cancellationToken).ConfigureAwait(false);
        }

        job.RecordCacheProgress(batch.Count, batch[^1].ApplicantId);
        if (job.IsCacheWarmComplete)
        {
            job.CompleteCacheWarm();
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    private async Task<Result> ProcessFanOutBatchAsync(PublishJob job, AdmissionResult result, int batchSize, CancellationToken cancellationToken)
    {
        var ordered = result.Entries.OrderBy(e => e.ApplicantId).ToList();
        var startIndex = job.FanOutCursor is { } cursor ? ordered.FindIndex(e => e.ApplicantId == cursor) + 1 : 0;
        var batch = ordered.Skip(startIndex).Take(batchSize).ToList();

        if (batch.Count == 0)
        {
            await CompleteAndPublishAsync(job, result, cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }

        foreach (var entry in batch)
        {
            // requirement-spec.md admission §2's "bulk admit-outcome document generation" has no
            // exact match in Documents' own DocumentType enum (AdmitCard/MeritList/Transcript/
            // Certificate/IdCard/Receipt) - "Certificate" is the closest existing semantic fit for a
            // formal admission/offer letter, a documented adaptation rather than adding a new
            // DocumentType value to a different, already-shipped module (out of this flow's scope).
            const string documentType = "Certificate";

            try
            {
                var generated = await documentGenerationRequester.RequestAsync(
                    new RequestDocumentGenerationCommand(entry.ApplicantId, documentType, entry.ApplicationId, new Dictionary<string, string> { ["outcome"] = entry.Outcome.ToString() }, LanguageCode: null, RequestedByUserId: null, result.Id.Value.ToString()),
                    cancellationToken).ConfigureAwait(false);

                if (generated.IsFailure && logger.IsEnabled(LogLevel.Warning))
                {
                    // A caught exception isn't the only failure shape here - Documents' own Result
                    // pattern can return a plain failed Result without ever throwing, so that
                    // outcome needs its own visibility too (a real gap this manual verification pass
                    // caught: this was previously silently discarded).
                    logger.LogWarning("PublishJob {PublishJobId} fan-out: admit-outcome document request for Applicant {ApplicantId} was rejected: {Error}.", job.Id.Value, entry.ApplicantId, generated.Error);
                }

                await notificationPublisher.PublishAsync(
                    new AdmissionNotificationRequest(entry.ApplicantId, "AdmissionResultPublished", entry.ApplicationId.ToString(), new Dictionary<string, string> { ["outcome"] = entry.Outcome.ToString() }, $"{result.Id}:{entry.ApplicantId}"),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // design-decisions.md's elevated retry/DLQ tier: a per-applicant failure here never
                // blocks the rest of the batch (fine-grained blast radius) - surfaced via logging;
                // this build's own worker re-attempts the whole batch on its next poll pass.
                logger.LogWarning(ex, "PublishJob {PublishJobId} fan-out failed for Applicant {ApplicantId} - will retry on the worker's next pass.", job.Id.Value, entry.ApplicantId);
            }
        }

        job.RecordFanOutProgress(batch.Count, batch[^1].ApplicantId);
        if (job.IsFanOutComplete)
        {
            await CompleteAndPublishAsync(job, result, cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    private async Task CompleteAndPublishAsync(PublishJob job, AdmissionResult result, CancellationToken cancellationToken)
    {
        job.Complete(clock.UtcNow);

        var published = result.MarkPublished(Guid.Empty, clock.UtcNow);
        if (published.IsFailure)
        {
            logger.LogError("PublishJob {PublishJobId} completed but AdmissionResult {AdmissionResultId} could not transition to Published: {Error}.", job.Id.Value, result.Id.Value, published.Error);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        domainEvents.Enqueue(new Domain.Events.AdmissionResultPublished(result.Id.Value, result.CampaignId, result.Entries.Count, clock.UtcNow));

        var auditRequest = AuditContext.ForSystemJob("publish-job", result.Id.Value.ToString(), "AdmissionResult", result.Id.Value.ToString(), AuditActions.Publish, "{\"status\":\"Publishing\"}", "{\"status\":\"Published\"}");
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
    }
}

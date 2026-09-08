using UMS.Modules.Content.Application.Abstractions;
using UMS.Modules.Content.Application.Common;
using UMS.Modules.Content.Domain.Common;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Content.Application.Scheduling;

/// <summary>
/// CNT-4: `publish_at &lt;= now AND status = Scheduled` scan-and-transition, and the mirror
/// `expire_at &lt;= now AND status = Published` scan. requirement-spec.md §4: "never a live
/// side effect" - only THIS service (called by <c>UMS.Workers</c>'s own polling
/// <c>BackgroundService</c>, never a request handler) performs the transition.
///
/// <para>
/// <b>No distributed lock</b> (design-decisions.md "Scheduled-Publish Job Exactly-Once Execution
/// Mechanism"): safe to run from more than one concurrent worker replica, because
/// <see cref="Notice.Publish"/>/<see cref="Notice.Archive"/>'s own optimistic-concurrency write is
/// what makes a second concurrent attempt against an already-transitioned row harmless -
/// <see cref="TransactionalAuditWriter.CommitWithAuditAsync"/> swallows the resulting
/// <see cref="ConcurrencyConflictException"/> into a Conflict <see cref="Result"/> this service
/// simply logs and moves past, never retries within the same tick.
/// </para>
///
/// <para>
/// The bilingual-completeness gate's THIRD independent call site (design-decisions.md): calling
/// the exact same <see cref="Notice.Publish"/> domain method the manual `/publish` endpoint calls
/// means the defensive re-check genuinely re-executes here, immediately before this commit -
/// edge-cases.md "A Notice's translation exists in only one language when publish_at fires": on
/// failure, the Notice is simply left in <see cref="SchedulableStatus.Scheduled"/> (the domain
/// method's failure Result means this loop never even attempts the save) for an Admin to
/// investigate, never force-published incomplete.
/// </para>
/// </summary>
public sealed class NoticeSchedulingService(INoticeRepository notices, IUnitOfWork unitOfWork, IAuditRecorder auditRecorder, ICacheInvalidator cacheInvalidator, IClock clock)
{
    private const string JobName = "content-notice-scheduler";

    /// <returns>How many Notices were successfully transitioned to Published this tick.</returns>
    public async Task<int> PublishDueAsync(int batchSize, string correlationId, CancellationToken cancellationToken = default)
    {
        var due = await notices.GetDueForPublishAsync(clock.UtcNow, batchSize, cancellationToken).ConfigureAwait(false);
        var publishedCount = 0;

        foreach (var notice in due)
        {
            var expectedVersion = notice.Version;
            var published = notice.Publish(clock.UtcNow);
            if (published.IsFailure)
            {
                // Bilingual-completeness gate failed defensively, or someone else already moved it
                // out of Scheduled - either way, nothing to commit; the row is left exactly as-is
                // for an Admin to investigate (edge-cases.md).
                continue;
            }

            unitOfWork.SetExpectedVersion(notice, expectedVersion);

            await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            var auditRequest = AuditContext.ForSystemJob(JobName, correlationId, "Notice", notice.Id.Value.ToString(), AuditActions.Publish, "{\"status\":\"Scheduled\"}", "{\"status\":\"Published\"}");
            var committed = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
            if (committed.IsFailure)
            {
                // design-decisions.md: lost the optimistic-concurrency race to a concurrent writer
                // (another job tick, or a human's edit) - harmless, move on to the next candidate.
                continue;
            }

            await cacheInvalidator.InvalidateAsync($"content:notice:{notice.Id.Value}", cancellationToken).ConfigureAwait(false);
            publishedCount++;
        }

        return publishedCount;
    }

    /// <returns>How many Notices were successfully transitioned to Archived this tick.</returns>
    public async Task<int> ExpireDueAsync(int batchSize, string correlationId, CancellationToken cancellationToken = default)
    {
        var due = await notices.GetDueForExpireAsync(clock.UtcNow, batchSize, cancellationToken).ConfigureAwait(false);
        var archivedCount = 0;

        foreach (var notice in due)
        {
            var expectedVersion = notice.Version;
            var archived = notice.Archive(clock.UtcNow);
            if (archived.IsFailure)
            {
                continue;
            }

            unitOfWork.SetExpectedVersion(notice, expectedVersion);

            await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            var auditRequest = AuditContext.ForSystemJob(JobName, correlationId, "Notice", notice.Id.Value.ToString(), AuditActions.Update, "{\"status\":\"Published\"}", "{\"status\":\"Archived\"}");
            var committed = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
            if (committed.IsFailure)
            {
                continue;
            }

            // edge-cases.md "CDN/Redis cache for a just-Archived notice is not invalidated in time":
            // best-effort only - NoticeService.GetByIdAsync's own Archived-check is the real
            // correctness backstop, independent of whether this call ever succeeds.
            await cacheInvalidator.InvalidateAsync($"content:notice:{notice.Id.Value}", cancellationToken).ConfigureAwait(false);
            archivedCount++;
        }

        return archivedCount;
    }
}

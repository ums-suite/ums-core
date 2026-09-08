using UMS.Modules.Content.Application.Abstractions;
using UMS.Modules.Content.Application.Common;
using UMS.Shared.Audit;

namespace UMS.Modules.Content.Application.Scheduling;

/// <summary>CNT-9: the identical idempotent, lock-free scan-and-transition as <see cref="NoticeSchedulingService"/>, reused for Banner (requirement-spec.md §2.3 "reusing the same background-job pattern").</summary>
public sealed class BannerSchedulingService(IBannerRepository banners, IUnitOfWork unitOfWork, IAuditRecorder auditRecorder, ICacheInvalidator cacheInvalidator, IClock clock)
{
    private const string JobName = "content-banner-scheduler";

    public async Task<int> PublishDueAsync(int batchSize, string correlationId, CancellationToken cancellationToken = default)
    {
        var due = await banners.GetDueForPublishAsync(clock.UtcNow, batchSize, cancellationToken).ConfigureAwait(false);
        var publishedCount = 0;

        foreach (var banner in due)
        {
            var expectedVersion = banner.Version;
            var published = banner.Publish(clock.UtcNow);
            if (published.IsFailure)
            {
                continue;
            }

            unitOfWork.SetExpectedVersion(banner, expectedVersion);

            await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            var auditRequest = AuditContext.ForSystemJob(JobName, correlationId, "Banner", banner.Id.Value.ToString(), AuditActions.Publish, "{\"status\":\"Scheduled\"}", "{\"status\":\"Published\"}");
            var committed = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
            if (committed.IsFailure)
            {
                continue;
            }

            await cacheInvalidator.InvalidateAsync("content:banners:active", cancellationToken).ConfigureAwait(false);
            publishedCount++;
        }

        return publishedCount;
    }

    public async Task<int> ExpireDueAsync(int batchSize, string correlationId, CancellationToken cancellationToken = default)
    {
        var due = await banners.GetDueForExpireAsync(clock.UtcNow, batchSize, cancellationToken).ConfigureAwait(false);
        var archivedCount = 0;

        foreach (var banner in due)
        {
            var expectedVersion = banner.Version;
            var archived = banner.Archive(clock.UtcNow);
            if (archived.IsFailure)
            {
                continue;
            }

            unitOfWork.SetExpectedVersion(banner, expectedVersion);

            await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            var auditRequest = AuditContext.ForSystemJob(JobName, correlationId, "Banner", banner.Id.Value.ToString(), AuditActions.Update, "{\"status\":\"Published\"}", "{\"status\":\"Archived\"}");
            var committed = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
            if (committed.IsFailure)
            {
                continue;
            }

            await cacheInvalidator.InvalidateAsync("content:banners:active", cancellationToken).ConfigureAwait(false);
            archivedCount++;
        }

        return archivedCount;
    }
}

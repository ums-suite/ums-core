using UMS.Modules.Content.Application.Abstractions;

namespace UMS.Modules.Content.Application.Scheduling;

/// <summary>CNT-11: the identical idempotent, lock-free scan-and-transition, reused for DownloadResource (requirement-spec.md §2.6 "reusing the Notice/Banner scheduling mechanism"). Not itself a 100%-audited entity per requirement-spec.md §5's Auditability list (Notice/Banner only) - no audit write here.</summary>
public sealed class DownloadResourceSchedulingService(IDownloadResourceRepository downloads, IUnitOfWork unitOfWork, IClock clock)
{
    public async Task<int> PublishDueAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        var due = await downloads.GetDueForPublishAsync(clock.UtcNow, batchSize, cancellationToken).ConfigureAwait(false);
        var publishedCount = 0;

        foreach (var resource in due)
        {
            var expectedVersion = resource.Version;
            var published = resource.Publish(clock.UtcNow);
            if (published.IsFailure)
            {
                continue;
            }

            unitOfWork.SetExpectedVersion(resource, expectedVersion);

            try
            {
                await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                publishedCount++;
            }
            catch (ConcurrencyConflictException)
            {
                // design-decisions.md: another writer won the race first - harmless, move on.
            }
        }

        return publishedCount;
    }

    public async Task<int> ExpireDueAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        var due = await downloads.GetDueForExpireAsync(clock.UtcNow, batchSize, cancellationToken).ConfigureAwait(false);
        var archivedCount = 0;

        foreach (var resource in due)
        {
            var expectedVersion = resource.Version;
            var archived = resource.Archive(clock.UtcNow);
            if (archived.IsFailure)
            {
                continue;
            }

            unitOfWork.SetExpectedVersion(resource, expectedVersion);

            try
            {
                await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                archivedCount++;
            }
            catch (ConcurrencyConflictException)
            {
            }
        }

        return archivedCount;
    }
}

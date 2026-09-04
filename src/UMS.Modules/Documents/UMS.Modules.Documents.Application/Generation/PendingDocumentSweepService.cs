using Microsoft.Extensions.Logging;
using UMS.Modules.Documents.Application.Abstractions;
using UMS.Modules.Documents.Domain.GeneratedDocuments;

namespace UMS.Modules.Documents.Application.Generation;

/// <summary>
/// edge-cases.md's object-storage/DB-ordering decision, residual note: "the compensating cleanup
/// job's own retry/backoff policy and how long a Pending row may sit before being swept is a
/// first-pass engineering default." Sweeps <see cref="GeneratedDocumentStatus.Pending"/>/
/// <see cref="GeneratedDocumentStatus.Uploaded"/> rows older than <see cref="StaleAfter"/> -
/// these are rows a crash or an unretried storage outage left stuck below <c>Ready</c> with no
/// outbox retry ever landing (DOC-15's own retry outbox message covers the common case; this is
/// the backstop for the rarer "the retry message itself was lost/never enqueued" case) - deletes
/// any attached (necessarily unverified) object-storage artifact and marks the row
/// <see cref="GeneratedDocumentStatus.Failed"/>, so its natural key becomes reclaimable by a fresh
/// caller request (<see cref="GeneratedDocument.Reopen"/>).
/// </summary>
public sealed class PendingDocumentSweepService(
    IGeneratedDocumentRepository documents,
    IObjectStorage objectStorage,
    IUnitOfWork unitOfWork,
    IClock clock,
    ILogger<PendingDocumentSweepService> logger)
{
    public static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(15);

    public async Task<int> SweepAsync(CancellationToken cancellationToken = default)
    {
        var stale = await documents.ListStalePendingAsync(clock.UtcNow - StaleAfter, cancellationToken).ConfigureAwait(false);

        foreach (var document in stale)
        {
            if (document.StorageKey is not null)
            {
                try
                {
                    await objectStorage.DeleteAsync(document.StorageKey, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "Sweep: failed to delete orphaned object {ObjectKey} for stale GeneratedDocument {DocumentId} - will retry next sweep.", document.StorageKey, document.Id);
                    continue;
                }
            }

            var failed = document.MarkFailed();
            if (failed.IsFailure)
            {
                logger.LogError("Sweep: unexpected MarkFailed failure for GeneratedDocument {DocumentId}: {Error}.", document.Id, failed.Error!.Code);
            }
        }

        if (stale.Count > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return stale.Count;
    }
}

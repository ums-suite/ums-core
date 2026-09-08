using UMS.Modules.Alumni.Application.Abstractions;
using UMS.Modules.Alumni.Application.Common;

namespace UMS.Modules.Alumni.Application.Jobs;

/// <summary>
/// ALM-6: the `expires_at &lt;= now AND status = Published` scheduled sweep (requirement-spec.md
/// §2.3) - mirrors Content's own Notice/Banner scheduling worker exactly (design-decisions.md item
/// 5): NO distributed lock/lease here - safe to run from multiple concurrent worker replicas because
/// <see cref="Domain.Jobs.JobPosting.ExpireIfDue"/>'s own guard (only a Published posting past its
/// own expires_at transitions) plus the uniform optimistic-concurrency Version column makes a
/// redundant concurrent tick harmless (a second racer's SaveChanges either finds nothing left to do
/// or hits a concurrency conflict that is simply swallowed - see
/// <see cref="TransactionalAuditWriter.CommitWithoutAuditAsync"/>).
/// </summary>
public sealed class JobPostingExpiryService(IJobPostingRepository postings, IUnitOfWork unitOfWork, IClock clock)
{
    public async Task<int> ExpireDueAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        var due = await postings.ListDueForExpiryAsync(clock.UtcNow, batchSize, cancellationToken).ConfigureAwait(false);
        var expiredCount = 0;

        foreach (var posting in due)
        {
            if (!posting.ExpireIfDue(clock.UtcNow))
            {
                continue;
            }

            await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            var committed = await TransactionalAuditWriter.CommitWithoutAuditAsync(unitOfWork, transaction, cancellationToken).ConfigureAwait(false);
            if (committed.IsSuccess)
            {
                expiredCount++;
            }
        }

        return expiredCount;
    }
}

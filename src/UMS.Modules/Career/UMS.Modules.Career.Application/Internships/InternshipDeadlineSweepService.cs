using UMS.Modules.Career.Application.Abstractions;
using UMS.Modules.Career.Application.Common;

namespace UMS.Modules.Career.Application.Internships;

/// <summary>
/// CAR-3: the `application_deadline &lt;= now AND status = ApplicationsOpen` scheduled sweep
/// (requirement-spec.md §2.2) - mirrors Alumni's own `JobPostingExpiryService` exactly. NO
/// distributed lock/lease here - safe to run from multiple concurrent worker replicas because
/// `Internship.CloseApplicationsIfDue`'s own guard plus the uniform optimistic-concurrency Version
/// column makes a redundant concurrent tick harmless.
/// </summary>
public sealed class InternshipDeadlineSweepService(IInternshipRepository internships, IUnitOfWork unitOfWork, IClock clock)
{
    public async Task<int> CloseDueAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        var due = await internships.ListDueForClosureAsync(clock.UtcNow, batchSize, cancellationToken).ConfigureAwait(false);
        var closedCount = 0;

        foreach (var internship in due)
        {
            if (!internship.CloseApplicationsIfDue(clock.UtcNow))
            {
                continue;
            }

            await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            var committed = await TransactionalAuditWriter.CommitWithoutAuditAsync(unitOfWork, transaction, cancellationToken).ConfigureAwait(false);
            if (committed.IsSuccess)
            {
                closedCount++;
            }
        }

        return closedCount;
    }
}

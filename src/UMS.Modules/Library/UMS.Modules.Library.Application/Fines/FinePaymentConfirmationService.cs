using UMS.Modules.Library.Application.Abstractions;

namespace UMS.Modules.Library.Application.Fines;

/// <summary>
/// LIB-13: Finance's own <c>PaymentSucceeded</c>/<c>PaymentFailed</c> domain events, filtered to
/// <c>Fine.InvoiceId</c> (requirement-spec.md §3's naming-reconciliation note: the spec's generic
/// "PaymentCompleted" term maps to these two actual events). Called by
/// <c>LibraryFinancePaymentRelayWorker</c> for every unprocessed event in Finance's outbox.
/// </summary>
public sealed class FinePaymentConfirmationService(IFineRepository fines, IUnitOfWork unitOfWork, IClock clock)
{
    /// <returns><see langword="true"/> if this event belonged to a Library Fine (processed or intentionally ignored); <see langword="false"/> if it matches no known Fine (a different module's own Finance usage).</returns>
    public async Task<bool> ApplyAsync(Guid invoiceId, string eventType, CancellationToken cancellationToken = default)
    {
        var fine = await fines.GetByInvoiceIdAsync(invoiceId, cancellationToken).ConfigureAwait(false);
        if (fine is null)
        {
            return false;
        }

        if (!string.Equals(eventType, "PaymentSucceeded", StringComparison.Ordinal))
        {
            // PaymentFailed: no state change - the Fine stays PendingSettlement and the borrower may
            // retry POST /fines/{id}/settle.
            return true;
        }

        var marked = fine.MarkPaid(clock.UtcNow);
        if (marked.IsSuccess)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return true;
    }
}

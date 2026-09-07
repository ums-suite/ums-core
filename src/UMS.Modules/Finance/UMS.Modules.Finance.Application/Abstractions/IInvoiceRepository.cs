using UMS.Modules.Finance.Domain.Invoices;

namespace UMS.Modules.Finance.Application.Abstractions;

public interface IInvoiceRepository
{
    public Task<Invoice?> GetByIdAsync(InvoiceId id, CancellationToken cancellationToken = default);

    /// <summary>design-decisions.md "Invoice-Level Concurrency Control for Payment Initiation": a pessimistic <c>SELECT ... FOR UPDATE</c> row lock, held for the duration of the Payment-creation transaction.</summary>
    public Task<Invoice?> GetByIdForUpdateAsync(InvoiceId id, CancellationToken cancellationToken = default);

    /// <summary>The dedup lookup edge-cases.md's "Concurrent CreateInvoice Calls" and requirement-spec.md §8's own sequential-retry case both rely on.</summary>
    public Task<Invoice?> GetByNaturalKeyAsync(string sourceModule, string sourceReferenceId, string feeType, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<Invoice>> GetByOwnerAsync(Guid ownerId, CancellationToken cancellationToken = default);

    public void Add(Invoice invoice);
}

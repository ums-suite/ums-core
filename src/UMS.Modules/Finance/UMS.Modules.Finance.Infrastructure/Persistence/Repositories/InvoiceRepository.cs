using Microsoft.EntityFrameworkCore;
using UMS.Modules.Finance.Application.Abstractions;
using UMS.Modules.Finance.Domain.Invoices;

namespace UMS.Modules.Finance.Infrastructure.Persistence.Repositories;

internal sealed class InvoiceRepository(FinanceDbContext context) : IInvoiceRepository
{
    public Task<Invoice?> GetByIdAsync(InvoiceId id, CancellationToken cancellationToken = default) =>
        Query().FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

    /// <summary>
    /// design-decisions.md "Invoice-Level Concurrency Control for Payment Initiation": a pessimistic
    /// row lock held for the duration of the Payment-creation transaction - mirrors Organization's
    /// own <c>CampusRepository.GetByIdForUpdateAsync</c> pattern in spirit, but as two steps, not
    /// one: a raw <c>SELECT ... FOR UPDATE</c> to take the lock (its own result set discarded),
    /// followed by a normal tracked LINQ query - both run inside the SAME caller-managed
    /// transaction, so the second query sees its own already-locked row. A bare
    /// <c>FromSqlInterpolated(...).SingleOrDefaultAsync()</c> root query against an entity with a
    /// <c>ComplexProperty</c> (<see cref="Invoice.TotalAmount"/>) breaks EF Core's own column-name
    /// resolution in this EF Core version (a real limitation, not a typo here) - a plain LINQ query
    /// has no such problem.
    /// </summary>
    public async Task<Invoice?> GetByIdForUpdateAsync(InvoiceId id, CancellationToken cancellationToken = default)
    {
        await context.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM finance.invoices WHERE id = {id.Value} FOR UPDATE", cancellationToken).ConfigureAwait(false);
        return await context.Invoices.FirstOrDefaultAsync(i => i.Id == id, cancellationToken).ConfigureAwait(false);
    }

    public Task<Invoice?> GetByNaturalKeyAsync(string sourceModule, string sourceReferenceId, string feeType, CancellationToken cancellationToken = default) =>
        Query().FirstOrDefaultAsync(i => i.SourceModule == sourceModule && i.SourceReferenceId == sourceReferenceId && i.FeeType == feeType, cancellationToken);

    public async Task<IReadOnlyList<Invoice>> GetByOwnerAsync(Guid ownerId, CancellationToken cancellationToken = default) =>
        await Query()
            .Where(i => i.OwnerId == ownerId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public void Add(Invoice invoice) => context.Invoices.Add(invoice);

    private IQueryable<Invoice> Query() => context.Invoices.Include(i => i.Items);
}

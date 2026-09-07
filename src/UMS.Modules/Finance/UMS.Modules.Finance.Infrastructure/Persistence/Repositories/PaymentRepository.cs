using Microsoft.EntityFrameworkCore;
using UMS.Modules.Finance.Application.Abstractions;
using UMS.Modules.Finance.Domain.Invoices;
using UMS.Modules.Finance.Domain.Payments;

namespace UMS.Modules.Finance.Infrastructure.Persistence.Repositories;

internal sealed class PaymentRepository(FinanceDbContext context) : IPaymentRepository
{
    public Task<Payment?> GetByIdAsync(PaymentId id, CancellationToken cancellationToken = default) =>
        Query().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    /// <summary>
    /// design-decisions.md's ordering-guarded state-transition function runs under this lock - the
    /// webhook handler and the stuck-payment sweep both acquire it before mutating. Two steps, not
    /// one: a raw <c>SELECT ... FOR UPDATE</c> to take the lock (its own result set discarded),
    /// followed by a normal tracked LINQ query with <c>.Include(p =&gt; p.Transactions)</c> - both run
    /// inside the SAME caller-managed transaction, so the second query sees its own already-locked
    /// row. Composing <c>.Include</c> directly on top of a raw <c>FromSqlInterpolated</c> root breaks
    /// EF Core's own column-name resolution for the <c>Amount</c> complex property in the generated
    /// wrapping query (a real EF Core limitation for this specific combination, not a typo here) -
    /// see <c>InvoiceRepository.GetByIdForUpdateAsync</c>'s own remarks for the same limitation hit
    /// the other way (dropping the Include instead, since Invoice's own callers never need Items).
    /// </summary>
    public async Task<Payment?> GetByIdForUpdateAsync(PaymentId id, CancellationToken cancellationToken = default)
    {
        await context.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM finance.payments WHERE id = {id.Value} FOR UPDATE", cancellationToken).ConfigureAwait(false);
        return await Query().FirstOrDefaultAsync(p => p.Id == id, cancellationToken).ConfigureAwait(false);
    }

    public Task<Payment?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default) =>
        Query().FirstOrDefaultAsync(p => p.IdempotencyKey == idempotencyKey, cancellationToken);

    public Task<Payment?> GetNonTerminalByInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken = default)
    {
        // Compares the whole strongly-typed InvoiceId, not a manually-unwrapped `.Value` - EF Core's
        // HasConversion-based translation for a strongly-typed id doesn't reliably translate a
        // member-access on the CONVERTED CLR type inside a predicate (a real, previously-hit EF Core
        // limitation, not a style preference).
        var typedInvoiceId = new InvoiceId(invoiceId);
        return Query().FirstOrDefaultAsync(p => p.InvoiceId == typedInvoiceId && (p.Status == PaymentStatus.Initiated || p.Status == PaymentStatus.Pending), cancellationToken);
    }

    public async Task<IReadOnlyList<Payment>> GetNonTerminalUpdatedBeforeAsync(DateTimeOffset updatedBefore, int batchSize, CancellationToken cancellationToken = default) =>
        await Query()
            .Where(p => (p.Status == PaymentStatus.Initiated || p.Status == PaymentStatus.Pending) && p.UpdatedAt < updatedBefore)
            .OrderBy(p => p.UpdatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public void Add(Payment payment) => context.Payments.Add(payment);

    private IQueryable<Payment> Query() => context.Payments.Include(p => p.Transactions);
}

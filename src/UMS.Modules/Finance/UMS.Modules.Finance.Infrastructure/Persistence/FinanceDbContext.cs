using System.Data.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using UMS.Modules.Finance.Application.Abstractions;
using UMS.Modules.Finance.Domain.Common;
using UMS.Modules.Finance.Domain.FeeStructures;
using UMS.Modules.Finance.Domain.Invoices;
using UMS.Modules.Finance.Domain.Ledger;
using UMS.Modules.Finance.Domain.Payments;
using UMS.Modules.Finance.Domain.Reconciliation;
using UMS.Shared.Outbox;

namespace UMS.Modules.Finance.Infrastructure.Persistence;

/// <summary>Finance's own schema (ADR-0004: <c>finance</c>) - mirrors Learning's/Academic's own DbContext shape exactly, including the outbox-drain-on-SaveChanges mechanism and the Postgres-exception translation pattern.</summary>
public sealed class FinanceDbContext(DbContextOptions<FinanceDbContext> options)
    : DbContext(options), IUnitOfWork, IDomainEventRecorder
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = false };

    private readonly List<IDomainEvent> _recordedEvents = [];

    internal DbSet<FeeStructure> FeeStructures => Set<FeeStructure>();

    internal DbSet<Invoice> Invoices => Set<Invoice>();

    internal DbSet<Payment> Payments => Set<Payment>();

    internal DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();

    internal DbSet<ReconciliationException> ReconciliationExceptions => Set<ReconciliationException>();

    internal DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public void Enqueue(IDomainEvent domainEvent) => _recordedEvents.Add(domainEvent);

    public async Task<IUmsTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        var transaction = await Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        return new EfUmsTransaction(transaction);
    }

    public void SetExpectedVersion<TEntity>(TEntity entity, uint expectedVersion)
        where TEntity : class =>
        Entry(entity).Property("Version").OriginalValue = expectedVersion;

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        AppendPendingDomainEventsToOutbox();

        try
        {
            return await base.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            var entityName = ex.Entries.Count > 0 ? ex.Entries[0].Entity.GetType().Name : "Entity";
            throw new ConcurrencyConflictException(entityName);
        }
        catch (DbUpdateException ex) when (TryTranslateUniqueViolation(ex, out var duplicateValueException))
        {
            throw duplicateValueException;
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("finance");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FinanceDbContext).Assembly);
    }

    private static bool TryTranslateUniqueViolation(DbUpdateException exception, out DuplicateValueException translated)
    {
        if (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgresException)
        {
            switch (postgresException.ConstraintName)
            {
                case "ux_fee_structures_active":
                    translated = new DuplicateValueException("FeeStructure", "feeType+applicability(Active)", "unknown");
                    return true;
                case "ux_invoices_natural_key":
                    translated = new DuplicateValueException("Invoice", "sourceModule+sourceReferenceId+feeType", "unknown");
                    return true;
                case "ux_payments_idempotency_key":
                    translated = new DuplicateValueException("Payment", "IdempotencyKey", "unknown");
                    return true;
                case "ux_payments_invoice_nonterminal":
                    translated = new DuplicateValueException("Payment", "InvoiceId(NonTerminal)", "unknown");
                    return true;
                case "ux_payment_transactions_gateway_transaction_id":
                    translated = new DuplicateValueException("PaymentTransaction", "GatewayTransactionId", "unknown");
                    return true;
                case "ux_refunds_id":
                    translated = new DuplicateValueException("Refund", "Id", "unknown");
                    return true;
            }
        }

        translated = null!;
        return false;
    }

    private void AppendPendingDomainEventsToOutbox()
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in ChangeTracker.Entries<IHasDomainEvents>().ToList())
        {
            foreach (var domainEvent in entry.Entity.DomainEvents)
            {
                _recordedEvents.Add(domainEvent);
            }

            entry.Entity.ClearDomainEvents();
        }

        foreach (var domainEvent in _recordedEvents)
        {
            OutboxMessages.Add(OutboxMessage.Create(
                domainEvent.GetType().Name,
                JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), SerializerOptions),
                domainEvent.OccurredAt,
                now));
        }

        _recordedEvents.Clear();
    }

    private sealed class EfUmsTransaction(IDbContextTransaction transaction) : IUmsTransaction
    {
        public DbTransaction DbTransaction => transaction.GetDbTransaction();

        public Task CommitAsync(CancellationToken cancellationToken = default) => transaction.CommitAsync(cancellationToken);

        public Task RollbackAsync(CancellationToken cancellationToken = default) => transaction.RollbackAsync(cancellationToken);

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}

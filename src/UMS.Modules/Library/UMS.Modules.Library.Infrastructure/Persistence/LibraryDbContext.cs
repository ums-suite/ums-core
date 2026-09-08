using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using UMS.Modules.Library.Application.Abstractions;
using UMS.Modules.Library.Domain.Catalog;
using UMS.Modules.Library.Domain.Common;
using UMS.Modules.Library.Domain.Fines;
using UMS.Modules.Library.Domain.Loans;
using UMS.Modules.Library.Domain.Reservations;
using UMS.Modules.Library.Infrastructure.Persistence.Inbox;
using UMS.Shared.Outbox;

namespace UMS.Modules.Library.Infrastructure.Persistence;

/// <summary>Library's own schema (ADR-0004: <c>library</c>) - mirrors every other module's own DbContext shape exactly, including the outbox-drain-on-SaveChanges mechanism and the Postgres-exception translation pattern.</summary>
public sealed class LibraryDbContext(DbContextOptions<LibraryDbContext> options)
    : DbContext(options), IUnitOfWork, IDomainEventRecorder
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = false };

    private readonly List<IDomainEvent> _recordedEvents = [];

    internal DbSet<Book> Books => Set<Book>();

    internal DbSet<Author> Authors => Set<Author>();

    internal DbSet<Category> Categories => Set<Category>();

    internal DbSet<BookCopy> BookCopies => Set<BookCopy>();

    internal DbSet<Loan> Loans => Set<Loan>();

    internal DbSet<LoanReviewFlag> LoanReviewFlags => Set<LoanReviewFlag>();

    internal DbSet<OverdueNotice> OverdueNotices => Set<OverdueNotice>();

    internal DbSet<Reservation> Reservations => Set<Reservation>();

    internal DbSet<BookReservationSupplyFlag> BookReservationSupplyFlags => Set<BookReservationSupplyFlag>();

    internal DbSet<Fine> Fines => Set<Fine>();

    internal DbSet<FineAccrual> FineAccruals => Set<FineAccrual>();

    internal DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    internal DbSet<ProcessedInboundEvent> ProcessedInboundEvents => Set<ProcessedInboundEvent>();

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
        modelBuilder.HasDefaultSchema("library");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LibraryDbContext).Assembly);
    }

    private static bool TryTranslateUniqueViolation(DbUpdateException exception, out DuplicateValueException translated)
    {
        if (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgresException)
        {
            switch (postgresException.ConstraintName)
            {
                case "ux_loans_bookcopy_active":
                    translated = new DuplicateValueException("Loan", "BookCopyId(Active)", "unknown");
                    return true;
                case "ux_fine_accruals_loan_date":
                    translated = new DuplicateValueException("FineAccrual", "LoanId+AccrualDate", "unknown");
                    return true;
                case "ux_overdue_notices_loan_date":
                    translated = new DuplicateValueException("OverdueNotice", "LoanId+NoticeDate", "unknown");
                    return true;
                case "ux_reservations_book_borrower_open":
                    translated = new DuplicateValueException("Reservation", "BookId+BorrowerId(Open)", "unknown");
                    return true;
                case "ux_book_copies_accession_number":
                    translated = new DuplicateValueException("BookCopy", "AccessionNumber", "unknown");
                    return true;
                case "ux_categories_name":
                    translated = new DuplicateValueException("Category", "Name", "unknown");
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
        public System.Data.Common.DbTransaction DbTransaction => transaction.GetDbTransaction();

        public Task CommitAsync(CancellationToken cancellationToken = default) => transaction.CommitAsync(cancellationToken);

        public Task RollbackAsync(CancellationToken cancellationToken = default) => transaction.RollbackAsync(cancellationToken);

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}

using UMS.Modules.Library.Domain.Catalog;

namespace UMS.Modules.Library.Application.Abstractions;

public interface IBookCopyRepository
{
    public Task<BookCopy?> GetByIdAsync(BookCopyId id, CancellationToken cancellationToken = default);

    /// <summary>design-decisions.md "Copy-Issuance Concurrency Control Pattern": the pessimistic lock every writer that changes a BookCopy's circulation state takes before mutating it. BookCopy has no owned collection needing `.Include`, so the single-step `FromSqlInterpolated ... FOR UPDATE` form suffices (mirrors Hostel's own `BedRepository`).</summary>
    public Task<BookCopy?> GetByIdForUpdateAsync(BookCopyId id, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<BookCopy>> GetByBookAsync(BookId bookId, CancellationToken cancellationToken = default);

    public Task<int> CountAvailableByBookAsync(BookId bookId, CancellationToken cancellationToken = default);

    /// <summary>LIB-17: every copy not yet Lost/Withdrawn - the "still theoretically fillable against the queue" denominator for <c>BookReservationSupplyFlag</c>'s own supply-vs-demand check.</summary>
    public Task<int> CountCirculatingByBookAsync(BookId bookId, CancellationToken cancellationToken = default);

    /// <summary>LIB-4: takes a row lock on every BookCopy of this Book (mirroring the same family of lock every other Copy-state writer takes) so Reservation creation's own "zero copies Available" check is genuinely transactional against a concurrent return/issuance, not a plain unlocked read.</summary>
    public Task LockAllByBookAsync(BookId bookId, CancellationToken cancellationToken = default);

    public void Add(BookCopy copy);
}

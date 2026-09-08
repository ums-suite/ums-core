using Microsoft.EntityFrameworkCore;
using UMS.Modules.Library.Application.Abstractions;
using UMS.Modules.Library.Domain.Catalog;

namespace UMS.Modules.Library.Infrastructure.Persistence.Repositories;

internal sealed class BookCopyRepository(LibraryDbContext context) : IBookCopyRepository
{
    private static readonly BookCopyStatus[] CirculatingStatuses = [BookCopyStatus.Available, BookCopyStatus.Reserved, BookCopyStatus.OnLoan];

    public Task<BookCopy?> GetByIdAsync(BookCopyId id, CancellationToken cancellationToken = default) =>
        context.BookCopies.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    /// <summary>design-decisions.md "Copy-Issuance Concurrency Control Pattern": no `.Include` needed on BookCopy, so the single-step `FromSqlInterpolated ... FOR UPDATE` form suffices (mirrors Hostel's own `BedRepository`).</summary>
    public Task<BookCopy?> GetByIdForUpdateAsync(BookCopyId id, CancellationToken cancellationToken = default) =>
        context.BookCopies
            .FromSqlInterpolated($"SELECT *, xmin FROM library.book_copies WHERE id = {id.Value} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<BookCopy>> GetByBookAsync(BookId bookId, CancellationToken cancellationToken = default) =>
        await context.BookCopies.Where(c => c.BookId == bookId).OrderBy(c => c.AccessionNumber).ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task<int> CountAvailableByBookAsync(BookId bookId, CancellationToken cancellationToken = default) =>
        context.BookCopies.CountAsync(c => c.BookId == bookId && c.Status == BookCopyStatus.Available, cancellationToken);

    public Task<int> CountCirculatingByBookAsync(BookId bookId, CancellationToken cancellationToken = default) =>
        context.BookCopies.CountAsync(c => c.BookId == bookId && CirculatingStatuses.Contains(c.Status), cancellationToken);

    /// <summary>LIB-4: locks every BookCopy row of this Book - the same lock family every other Copy-state writer takes - so Reservation-creation's own availability check is genuinely transactional.</summary>
    public async Task LockAllByBookAsync(BookId bookId, CancellationToken cancellationToken = default) =>
        await context.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM library.book_copies WHERE book_id = {bookId.Value} FOR UPDATE", cancellationToken).ConfigureAwait(false);

    public void Add(BookCopy copy) => context.BookCopies.Add(copy);
}

using Microsoft.EntityFrameworkCore;
using UMS.Modules.Library.Domain.Fines;
using UMS.Modules.Library.Domain.Loans;
using UMS.Modules.Library.Infrastructure.Persistence;
using UMS.Shared.Library;

namespace UMS.Modules.Library.Infrastructure.CrossModule;

/// <summary>
/// RPT-3/RPT-9: the one real implementation of <see cref="ILibraryReportingQuery"/> - mirrors
/// <c>UMS.Modules.Academic.Infrastructure.CrossModule.AcademicReportingQueryAdapter</c>'s exact
/// pattern.
/// </summary>
internal sealed class LibraryReportingQueryAdapter(LibraryDbContext context) : ILibraryReportingQuery
{
    private const int MostBorrowedTake = 10;

    public async Task<LibraryDashboardSnapshot> GetDashboardSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var totalBooks = await context.Books.CountAsync(cancellationToken).ConfigureAwait(false);
        var totalBookCopies = await context.BookCopies.CountAsync(cancellationToken).ConfigureAwait(false);

        var activeLoanCount = await context.Loans.CountAsync(l => l.Status == LoanStatus.Active, cancellationToken).ConfigureAwait(false);

        var utcNow = DateTimeOffset.UtcNow;
        var overdueLoanCount = await context.Loans
            .CountAsync(l => l.Status == LoanStatus.Active && l.DueDate < utcNow, cancellationToken)
            .ConfigureAwait(false);

        var totalOutstandingFines = await context.Fines
            .Where(f => f.Status == FineStatus.Accruing || f.Status == FineStatus.PendingSettlement)
            .SumAsync(f => f.Amount.Amount, cancellationToken).ConfigureAwait(false);

        var loanCountsByBook = await context.Loans
            .GroupBy(l => l.BookId)
            .Select(g => new { BookId = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .Take(MostBorrowedTake)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var bookIds = loanCountsByBook.Select(g => g.BookId).ToList();
        var titlesById = await context.Books
            .Where(b => bookIds.Contains(b.Id.Value))
            .Select(b => new { Id = b.Id.Value, b.Title })
            .ToDictionaryAsync(b => b.Id, b => b.Title, cancellationToken).ConfigureAwait(false);

        var mostBorrowedBooks = loanCountsByBook
            .Select(g => new MostBorrowedBook(g.BookId, titlesById.GetValueOrDefault(g.BookId, "Unknown"), g.Count))
            .ToList();

        return new LibraryDashboardSnapshot(totalBooks, totalBookCopies, activeLoanCount, overdueLoanCount, totalOutstandingFines, mostBorrowedBooks);
    }
}

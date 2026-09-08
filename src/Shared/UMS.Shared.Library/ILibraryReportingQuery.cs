namespace UMS.Shared.Library;

/// <summary>
/// RPT-3/RPT-9: mirrors <c>UMS.Shared.Academic.IAcademicReportingQuery</c>'s exact pattern - lives
/// here (not <c>UMS.Modules.Library.*</c>) so Reporting can call it without a forbidden dependency
/// on Library's internals (module-boundaries.md, ADR-0002). Library's own Infrastructure layer
/// registers the one real implementation.
///
/// <para>
/// No <c>asOf</c> parameter - see <c>UMS.Shared.Academic.IAcademicReportingQuery</c>'s own remarks
/// for the documented read-consistency simplification.
/// </para>
/// </summary>
public interface ILibraryReportingQuery
{
    /// <summary>RPT-9: books, active loans, overdue books, fines, most-borrowed.</summary>
    public Task<LibraryDashboardSnapshot> GetDashboardSnapshotAsync(CancellationToken cancellationToken = default);
}

public sealed record LibraryDashboardSnapshot(
    int TotalBooks,
    int TotalBookCopies,
    int ActiveLoanCount,
    int OverdueLoanCount,
    decimal TotalOutstandingFines,
    IReadOnlyList<MostBorrowedBook> MostBorrowedBooks);

public sealed record MostBorrowedBook(Guid BookId, string Title, int LoanCount);

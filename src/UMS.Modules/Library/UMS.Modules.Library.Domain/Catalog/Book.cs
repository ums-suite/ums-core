using UMS.Modules.Library.Domain.Common;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Library.Domain.Catalog;

/// <summary>
/// LIB-1: requirement-spec.md §3 - "the practical catalog root for this spec's operations" (§9
/// decision 1: no separate <c>Catalog</c> aggregate exists). <see cref="AuthorIds"/> is a plain Guid
/// list, not a navigation to <see cref="Author"/> rows, mirroring Hostel's own
/// <c>ApplicationWindow.EligibleProgramIds</c> precedent (Infrastructure's own
/// <c>JsonListConverters</c>) - nothing in this module ever queries/joins on one individual author
/// id, so a jsonb column costs nothing here versus a many-to-many join table.
///
/// <para>
/// <see cref="IsOpenAccessDigital"/> is requirement-spec.md §2's "Open-access digital resources
/// (unlimited seats) skip the Loan machinery entirely and are served as a direct, always-Available
/// access grant" - a Book flagged this way is expected to have zero <see cref="BookCopy"/> rows;
/// a Book with one or more <c>Digital</c>-type copies instead reuses the ordinary scarce-resource
/// Loan/Reservation machinery per §9 decision 4.
/// </para>
///
/// <para>
/// <see cref="Withdraw"/> only blocks NEW Loans/Reservations (requirement-spec.md §8: "A Book is
/// withdrawn from the catalog while copies are on loan → existing loans are unaffected") - it does
/// not touch any already-issued <see cref="Loan"/> or already-queued <see cref="Reservation"/>.
/// </para>
/// </summary>
public sealed class Book : AggregateRoot<BookId>
{
    private Book()
    {
    }

    private Book(BookId id, string title, string? isbn, CategoryId? categoryId, IReadOnlyCollection<Guid> authorIds, string? edition, bool isOpenAccessDigital, DateTimeOffset now)
    {
        Id = id;
        Title = title;
        Isbn = isbn;
        CategoryId = categoryId;
        AuthorIds = authorIds;
        Edition = edition;
        IsOpenAccessDigital = isOpenAccessDigital;
        CreatedAt = now;
    }

    public string Title { get; private set; } = string.Empty;

    public string? Isbn { get; private set; }

    public CategoryId? CategoryId { get; private set; }

    public IReadOnlyCollection<Guid> AuthorIds { get; private set; } = [];

    public string? Edition { get; private set; }

    public bool IsOpenAccessDigital { get; private set; }

    public bool Withdrawn { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? WithdrawnAt { get; private set; }

    public static Result<Book> Create(string title, string? isbn, CategoryId? categoryId, IReadOnlyCollection<Guid>? authorIds, string? edition, bool isOpenAccessDigital, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return Error.Validation("book.title_required", "A Book's title is required.");
        }

        return new Book(BookId.New(), title.Trim(), NormalizeIsbn(isbn), categoryId, authorIds ?? [], edition?.Trim(), isOpenAccessDigital, now);
    }

    public Result UpdateDetails(string title, string? isbn, CategoryId? categoryId, IReadOnlyCollection<Guid>? authorIds, string? edition)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return Result.Failure(Error.Validation("book.title_required", "A Book's title is required."));
        }

        Title = title.Trim();
        Isbn = NormalizeIsbn(isbn);
        CategoryId = categoryId;
        AuthorIds = authorIds ?? [];
        Edition = edition?.Trim();
        return Result.Success();
    }

    public Result Withdraw(DateTimeOffset now)
    {
        if (Withdrawn)
        {
            return Result.Failure(Error.Conflict("book.already_withdrawn", $"Book '{Id}' is already withdrawn."));
        }

        Withdrawn = true;
        WithdrawnAt = now;
        return Result.Success();
    }

    public Result Reinstate()
    {
        if (!Withdrawn)
        {
            return Result.Failure(Error.Conflict("book.not_withdrawn", $"Book '{Id}' is not withdrawn."));
        }

        Withdrawn = false;
        WithdrawnAt = null;
        return Result.Success();
    }

    private static string? NormalizeIsbn(string? isbn) => string.IsNullOrWhiteSpace(isbn) ? null : isbn.Trim();
}

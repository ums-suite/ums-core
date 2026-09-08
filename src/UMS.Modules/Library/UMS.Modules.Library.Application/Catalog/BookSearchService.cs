using System.Text;
using UMS.Modules.Library.Application.Abstractions;
using UMS.Modules.Library.Domain.Catalog;

namespace UMS.Modules.Library.Application.Catalog;

/// <summary>
/// LIB-2: <c>GET /books</c> - requirement-spec.md §2 Catalog Management (p95 &lt; 1s), §8 edge case
/// "Bengali-titled book search must match correctly regardless of Unicode normalization form used by
/// the client" (ADR-0011).
///
/// <para>
/// <b>Bengali Unicode normalization - a genuinely new mechanism in this codebase, not a copied
/// precedent.</b> Nothing else in this repo does search-time Unicode normalization (no
/// <c>NormalizationForm</c>, <c>ILIKE</c>/<c>tsvector</c>/<c>COLLATE</c>/<c>icu_</c> usage exists
/// anywhere else). The first-pass approach taken here: normalize the incoming query string to NFC
/// (<see cref="string.Normalize(NormalizationForm)"/>) in this C# service layer, and normalize
/// <see cref="Book.Title"/>/<see cref="Author.Name"/> to NFC at write time
/// (<c>CatalogService</c>'s own <c>Create</c>/<c>UpdateDetails</c> calls do NOT currently do this -
/// see the PR description's own "known gaps" list) so a client sending a decomposed (NFD) Bengali
/// string still matches a title stored/typed in precomposed (NFC) form. Comparison itself still runs
/// as an ordinary case-insensitive <c>ILIKE</c>-equivalent substring match in the repository -
/// Postgres's default collation does not itself normalize Unicode forms, which is exactly why this
/// normalization step has to happen in code, not in SQL. Documented as a first-pass decision with no
/// prior pattern to validate against, not a proven, battle-tested mechanism.
/// </para>
/// </summary>
public sealed class BookSearchService(IBookRepository books, IAuthorRepository authors, ICategoryRepository categories, IBookCopyRepository bookCopies)
{
    public async Task<BookSearchPageDto> SearchAsync(string? searchText, Guid? categoryId, Guid? authorId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var normalizedSearchText = Normalize(searchText);
        var skip = Math.Max(0, page - 1) * pageSize;

        var matches = await books.SearchAsync(normalizedSearchText, categoryId, authorId, skip, pageSize, cancellationToken).ConfigureAwait(false);
        var totalCount = await books.CountSearchAsync(normalizedSearchText, categoryId, authorId, cancellationToken).ConfigureAwait(false);

        var results = new List<BookSearchResultDto>(matches.Count);
        foreach (var book in matches)
        {
            var categoryName = book.CategoryId is { } catId ? (await categories.GetByIdAsync(catId, cancellationToken).ConfigureAwait(false))?.Name : null;
            var authorNames = book.AuthorIds.Count == 0
                ? []
                : (await authors.GetByIdsAsync(book.AuthorIds, cancellationToken).ConfigureAwait(false)).Select(a => a.Name).ToList();
            var availableCount = await bookCopies.CountAvailableByBookAsync(book.Id, cancellationToken).ConfigureAwait(false);
            var copies = await bookCopies.GetByBookAsync(book.Id, cancellationToken).ConfigureAwait(false);

            results.Add(new BookSearchResultDto(book.Id.Value, book.Title, book.Isbn, categoryName, authorNames, availableCount, copies.Count, book.Withdrawn));
        }

        return new BookSearchPageDto(results, totalCount, page, pageSize);
    }

    /// <summary>NFC-normalizes for comparison - see class remarks. Blank/whitespace-only input is treated as "no filter."</summary>
    internal static string? Normalize(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Trim().Normalize(NormalizationForm.FormC);
}

public sealed record BookSearchResultDto(Guid Id, string Title, string? Isbn, string? CategoryName, IReadOnlyList<string> AuthorNames, int AvailableCopyCount, int TotalCopyCount, bool Withdrawn);

public sealed record BookSearchPageDto(IReadOnlyList<BookSearchResultDto> Items, int TotalCount, int Page, int PageSize);

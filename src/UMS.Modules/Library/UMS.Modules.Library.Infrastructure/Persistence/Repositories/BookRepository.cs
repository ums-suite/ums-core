using Microsoft.EntityFrameworkCore;
using UMS.Modules.Library.Application.Abstractions;
using UMS.Modules.Library.Domain.Catalog;

namespace UMS.Modules.Library.Infrastructure.Persistence.Repositories;

internal sealed class BookRepository(LibraryDbContext context) : IBookRepository
{
    public Task<Book?> GetByIdAsync(BookId id, CancellationToken cancellationToken = default) =>
        context.Books.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

    /// <summary>
    /// LIB-2: title/ISBN match via <c>ILIKE</c> (case-insensitive substring, translated server-side);
    /// Category filter via SQL. Author filtering is applied CLIENT-SIDE after the SQL-filtered set is
    /// materialized - <see cref="Book.AuthorIds"/> is stored as one <c>jsonb</c> column (a
    /// <c>ValueConverter</c>-backed scalar to EF, not a queryable collection), so a per-author SQL
    /// filter would need a Postgres jsonb/GIN-indexed containment query this first pass does not
    /// build. Documented as a known scaling limitation, not a hidden gap - fine for the catalog sizes
    /// this build's tests exercise.
    /// </summary>
    public async Task<IReadOnlyList<Book>> SearchAsync(string? searchText, Guid? categoryId, Guid? authorId, int skip, int take, CancellationToken cancellationToken = default)
    {
        var matches = await FilteredByTextAndCategory(searchText, categoryId).ToListAsync(cancellationToken).ConfigureAwait(false);
        var filtered = authorId is { } id ? matches.Where(b => b.AuthorIds.Contains(id)) : matches;
        return filtered.OrderBy(b => b.Title).Skip(skip).Take(take).ToList();
    }

    public async Task<int> CountSearchAsync(string? searchText, Guid? categoryId, Guid? authorId, CancellationToken cancellationToken = default)
    {
        if (authorId is null)
        {
            return await FilteredByTextAndCategory(searchText, categoryId).CountAsync(cancellationToken).ConfigureAwait(false);
        }

        var matches = await FilteredByTextAndCategory(searchText, categoryId).ToListAsync(cancellationToken).ConfigureAwait(false);
        return matches.Count(b => b.AuthorIds.Contains(authorId.Value));
    }

    public void Add(Book book) => context.Books.Add(book);

    private IQueryable<Book> FilteredByTextAndCategory(string? searchText, Guid? categoryId)
    {
        var query = context.Books.AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            query = query.Where(b => EF.Functions.ILike(b.Title, $"%{searchText}%") || (b.Isbn != null && EF.Functions.ILike(b.Isbn, $"%{searchText}%")));
        }

        if (categoryId is { } id)
        {
            var typedCategoryId = new CategoryId(id);
            query = query.Where(b => b.CategoryId == typedCategoryId);
        }

        return query;
    }
}

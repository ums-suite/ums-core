using UMS.Modules.Library.Domain.Catalog;

namespace UMS.Modules.Library.Application.Abstractions;

public interface IBookRepository
{
    public Task<Book?> GetByIdAsync(BookId id, CancellationToken cancellationToken = default);

    /// <summary>LIB-2: a plain, unlocked read for catalog search/browse - performance-sensitive (p95 < 1s), never inside a write transaction.</summary>
    public Task<IReadOnlyList<Book>> SearchAsync(string? searchText, Guid? categoryId, Guid? authorId, int skip, int take, CancellationToken cancellationToken = default);

    public Task<int> CountSearchAsync(string? searchText, Guid? categoryId, Guid? authorId, CancellationToken cancellationToken = default);

    public void Add(Book book);
}

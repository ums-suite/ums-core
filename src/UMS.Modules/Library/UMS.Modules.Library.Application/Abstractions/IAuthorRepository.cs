using UMS.Modules.Library.Domain.Catalog;

namespace UMS.Modules.Library.Application.Abstractions;

public interface IAuthorRepository
{
    public Task<Author?> GetByIdAsync(AuthorId id, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<Author>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<Author>> SearchByNameAsync(string? searchText, int take, CancellationToken cancellationToken = default);

    public void Add(Author author);
}

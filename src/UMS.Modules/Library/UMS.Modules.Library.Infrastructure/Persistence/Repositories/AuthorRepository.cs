using Microsoft.EntityFrameworkCore;
using UMS.Modules.Library.Application.Abstractions;
using UMS.Modules.Library.Domain.Catalog;

namespace UMS.Modules.Library.Infrastructure.Persistence.Repositories;

internal sealed class AuthorRepository(LibraryDbContext context) : IAuthorRepository
{
    public Task<Author?> GetByIdAsync(AuthorId id, CancellationToken cancellationToken = default) =>
        context.Authors.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Author>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        var typedIds = ids.Select(id => new AuthorId(id)).ToList();
        return await context.Authors.Where(a => typedIds.Contains(a.Id)).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Author>> SearchByNameAsync(string? searchText, int take, CancellationToken cancellationToken = default)
    {
        var query = context.Authors.AsQueryable();
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            query = query.Where(a => EF.Functions.ILike(a.Name, $"%{searchText}%"));
        }

        return await query.OrderBy(a => a.Name).Take(take).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public void Add(Author author) => context.Authors.Add(author);
}

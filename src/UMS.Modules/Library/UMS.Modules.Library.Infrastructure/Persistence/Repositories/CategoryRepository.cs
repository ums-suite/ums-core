using Microsoft.EntityFrameworkCore;
using UMS.Modules.Library.Application.Abstractions;
using UMS.Modules.Library.Domain.Catalog;

namespace UMS.Modules.Library.Infrastructure.Persistence.Repositories;

internal sealed class CategoryRepository(LibraryDbContext context) : ICategoryRepository
{
    public Task<Category?> GetByIdAsync(CategoryId id, CancellationToken cancellationToken = default) =>
        context.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await context.Categories.OrderBy(c => c.Name).ToListAsync(cancellationToken).ConfigureAwait(false);

    public void Add(Category category) => context.Categories.Add(category);
}

using UMS.Modules.Library.Domain.Catalog;

namespace UMS.Modules.Library.Application.Abstractions;

public interface ICategoryRepository
{
    public Task<Category?> GetByIdAsync(CategoryId id, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default);

    public void Add(Category category);
}

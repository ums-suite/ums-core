using Microsoft.EntityFrameworkCore;
using UMS.Modules.Content.Application.Abstractions;
using UMS.Modules.Content.Domain.HomepageSections;

namespace UMS.Modules.Content.Infrastructure.Persistence.Repositories;

internal sealed class HomepageSectionRepository(ContentDbContext context) : IHomepageSectionRepository
{
    public Task<HomepageSection?> GetByIdAsync(HomepageSectionId id, CancellationToken cancellationToken = default) =>
        context.HomepageSections.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public void Add(HomepageSection section) => context.HomepageSections.Add(section);

    public async Task<IReadOnlyList<HomepageSection>> ListEnabledOrderedAsync(CancellationToken cancellationToken = default) =>
        await context.HomepageSections.Where(s => s.IsEnabled).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<HomepageSection>> ListAllOrderedAsync(CancellationToken cancellationToken = default) =>
        await context.HomepageSections.ToListAsync(cancellationToken).ConfigureAwait(false);
}

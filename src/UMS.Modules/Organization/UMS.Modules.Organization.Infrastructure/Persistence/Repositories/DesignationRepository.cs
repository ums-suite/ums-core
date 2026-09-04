using Microsoft.EntityFrameworkCore;
using UMS.Modules.Organization.Application.Abstractions;
using UMS.Modules.Organization.Domain.Designations;

namespace UMS.Modules.Organization.Infrastructure.Persistence.Repositories;

internal sealed class DesignationRepository(OrganizationDbContext context) : IDesignationRepository
{
    public Task<Designation?> GetByIdAsync(DesignationId id, CancellationToken cancellationToken = default) =>
        context.Designations.Include(d => d.Translations).FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public Task<bool> ExistsByTitleAsync(string title, CancellationToken cancellationToken = default) =>
        context.Designations.AnyAsync(d => d.Title == title, cancellationToken);

    public async Task<IReadOnlyList<Designation>> ListAsync(int skip, int take, CancellationToken cancellationToken = default) =>
        await context.Designations
            .Include(d => d.Translations)
            .OrderBy(d => d.Title)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<int> CountAsync(CancellationToken cancellationToken = default) => context.Designations.CountAsync(cancellationToken);

    public void Add(Designation designation) => context.Designations.Add(designation);
}

using Microsoft.EntityFrameworkCore;
using UMS.Modules.Organization.Application.Abstractions;
using UMS.Modules.Organization.Domain.Universities;

namespace UMS.Modules.Organization.Infrastructure.Persistence.Repositories;

internal sealed class UniversityRepository(OrganizationDbContext context) : IUniversityRepository
{
    public Task<University?> GetByIdAsync(UniversityId id, CancellationToken cancellationToken = default) =>
        context.Universities.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    /// <summary>design-decisions.md, "Parent-Active-Status Validation Mechanism": `SELECT ... FOR UPDATE`, held for the duration of the caller's own check-plus-mutation.</summary>
    public Task<University?> GetByIdForUpdateAsync(UniversityId id, CancellationToken cancellationToken = default) =>
        context.Universities
            .FromSqlInterpolated($"SELECT *, xmin FROM organization.universities WHERE id = {id.Value} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<University>> ListAsync(int skip, int take, CancellationToken cancellationToken = default) =>
        await context.Universities
            .OrderBy(u => u.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<int> CountAsync(CancellationToken cancellationToken = default) => context.Universities.CountAsync(cancellationToken);

    public void Add(University university) => context.Universities.Add(university);
}

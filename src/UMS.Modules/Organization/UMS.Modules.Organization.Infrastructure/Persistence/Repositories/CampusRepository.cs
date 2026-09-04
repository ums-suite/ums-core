using Microsoft.EntityFrameworkCore;
using UMS.Modules.Organization.Application.Abstractions;
using UMS.Modules.Organization.Domain.Campuses;
using UMS.Modules.Organization.Domain.Common;
using UMS.Modules.Organization.Domain.Universities;

namespace UMS.Modules.Organization.Infrastructure.Persistence.Repositories;

internal sealed class CampusRepository(OrganizationDbContext context) : ICampusRepository
{
    public Task<Campus?> GetByIdAsync(CampusId id, CancellationToken cancellationToken = default) =>
        context.Campuses.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<Campus?> GetByIdForUpdateAsync(CampusId id, CancellationToken cancellationToken = default) =>
        context.Campuses
            .FromSqlInterpolated($"SELECT *, xmin FROM organization.campuses WHERE id = {id.Value} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

    public Task<bool> HasAnyActiveUnderAsync(UniversityId universityId, CancellationToken cancellationToken = default) =>
        context.Campuses.AnyAsync(c => c.UniversityId == universityId && c.Status == NodeStatus.Active, cancellationToken);

    public async Task<IReadOnlyList<Campus>> ListAsync(UniversityId? universityId, int skip, int take, CancellationToken cancellationToken = default) =>
        await context.Campuses
            .Where(c => universityId == null || c.UniversityId == universityId.Value)
            .OrderBy(c => c.Name)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<int> CountAsync(UniversityId? universityId, CancellationToken cancellationToken = default) =>
        context.Campuses.CountAsync(c => universityId == null || c.UniversityId == universityId.Value, cancellationToken);

    public void Add(Campus campus) => context.Campuses.Add(campus);
}

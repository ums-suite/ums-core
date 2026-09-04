using Microsoft.EntityFrameworkCore;
using UMS.Modules.Organization.Application.Abstractions;
using UMS.Modules.Organization.Domain.Campuses;
using UMS.Modules.Organization.Domain.Facilities;

namespace UMS.Modules.Organization.Infrastructure.Persistence.Repositories;

internal sealed class BuildingRepository(OrganizationDbContext context) : IBuildingRepository
{
    public Task<Building?> GetByIdAsync(BuildingId id, CancellationToken cancellationToken = default) =>
        context.Buildings.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

    public Task<Building?> GetByIdForUpdateAsync(BuildingId id, CancellationToken cancellationToken = default) =>
        context.Buildings
            .FromSqlInterpolated($"SELECT *, xmin FROM organization.buildings WHERE id = {id.Value} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<Building>> ListAsync(CampusId? campusId, int skip, int take, CancellationToken cancellationToken = default) =>
        await context.Buildings
            .Where(b => campusId == null || b.CampusId == campusId.Value)
            .OrderBy(b => b.Name)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<int> CountAsync(CampusId? campusId, CancellationToken cancellationToken = default) =>
        context.Buildings.CountAsync(b => campusId == null || b.CampusId == campusId.Value, cancellationToken);

    public void Add(Building building) => context.Buildings.Add(building);

    public void Remove(Building building) => context.Buildings.Remove(building);
}

using Microsoft.EntityFrameworkCore;
using UMS.Modules.Organization.Application.Abstractions;
using UMS.Modules.Organization.Domain.Campuses;
using UMS.Modules.Organization.Domain.Common;
using UMS.Modules.Organization.Domain.Faculties;

namespace UMS.Modules.Organization.Infrastructure.Persistence.Repositories;

internal sealed class FacultyRepository(OrganizationDbContext context) : IFacultyRepository
{
    public Task<Faculty?> GetByIdAsync(FacultyId id, CancellationToken cancellationToken = default) =>
        context.Faculties.Include(f => f.Translations).FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

    public Task<Faculty?> GetByIdForUpdateAsync(FacultyId id, CancellationToken cancellationToken = default) =>
        context.Faculties
            .FromSqlInterpolated($"SELECT *, xmin FROM organization.faculties WHERE id = {id.Value} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

    public Task<bool> HasAnyActiveUnderAsync(CampusId campusId, CancellationToken cancellationToken = default) =>
        context.Faculties.AnyAsync(f => f.CampusId == campusId && f.Status == NodeStatus.Active, cancellationToken);

    public async Task<IReadOnlyList<Faculty>> ListAsync(CampusId? campusId, int skip, int take, CancellationToken cancellationToken = default) =>
        await context.Faculties
            .Include(f => f.Translations)
            .Where(f => campusId == null || f.CampusId == campusId.Value)
            .OrderBy(f => f.Name)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<int> CountAsync(CampusId? campusId, CancellationToken cancellationToken = default) =>
        context.Faculties.CountAsync(f => campusId == null || f.CampusId == campusId.Value, cancellationToken);

    public async Task<IReadOnlyList<Faculty>> ListAllAsync(CancellationToken cancellationToken = default) =>
        await context.Faculties.Include(f => f.Translations).ToListAsync(cancellationToken).ConfigureAwait(false);

    public void Add(Faculty faculty) => context.Faculties.Add(faculty);
}

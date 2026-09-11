using Microsoft.EntityFrameworkCore;
using UMS.Modules.Career.Application.Abstractions;
using UMS.Modules.Career.Domain.Employers;

namespace UMS.Modules.Career.Infrastructure.Persistence.Repositories;

internal sealed class EmployerProfileRepository(CareerDbContext context) : IEmployerProfileRepository
{
    public Task<EmployerProfile?> GetByIdAsync(EmployerProfileId id, CancellationToken cancellationToken = default) =>
        context.EmployerProfiles.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task<IReadOnlyList<EmployerProfile>> ListAsync(bool includeArchived, int skip, int take, CancellationToken cancellationToken = default)
    {
        var query = context.EmployerProfiles.AsQueryable();
        if (!includeArchived)
        {
            query = query.Where(e => !e.IsArchived);
        }

        return await query.OrderBy(e => e.CompanyName).Skip(skip).Take(take).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public void Add(EmployerProfile profile) => context.EmployerProfiles.Add(profile);
}

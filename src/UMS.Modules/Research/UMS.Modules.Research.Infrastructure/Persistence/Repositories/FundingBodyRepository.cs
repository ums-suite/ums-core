using Microsoft.EntityFrameworkCore;
using UMS.Modules.Research.Application.Abstractions;
using UMS.Modules.Research.Domain.FundingBodies;
using UMS.Modules.Research.Infrastructure.Persistence;

namespace UMS.Modules.Research.Infrastructure.Persistence.Repositories;

internal sealed class FundingBodyRepository(ResearchDbContext context) : IFundingBodyRepository
{
    public Task<FundingBody?> GetByIdAsync(FundingBodyId id, CancellationToken cancellationToken = default) =>
        context.FundingBodies.FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

    public async Task<IReadOnlyList<FundingBody>> ListAsync(int skip, int take, CancellationToken cancellationToken = default) =>
        await context.FundingBodies
            .OrderBy(f => f.Name)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public void Add(FundingBody fundingBody) => context.FundingBodies.Add(fundingBody);
}

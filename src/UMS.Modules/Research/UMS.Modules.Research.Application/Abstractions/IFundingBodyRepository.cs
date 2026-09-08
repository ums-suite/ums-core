using UMS.Modules.Research.Domain.FundingBodies;

namespace UMS.Modules.Research.Application.Abstractions;

public interface IFundingBodyRepository
{
    public Task<FundingBody?> GetByIdAsync(FundingBodyId id, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<FundingBody>> ListAsync(int skip, int take, CancellationToken cancellationToken = default);

    public void Add(FundingBody fundingBody);
}

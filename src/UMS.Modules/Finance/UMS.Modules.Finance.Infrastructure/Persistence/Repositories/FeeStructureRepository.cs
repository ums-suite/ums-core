using Microsoft.EntityFrameworkCore;
using UMS.Modules.Finance.Application.Abstractions;
using UMS.Modules.Finance.Domain.FeeStructures;
using UMS.Modules.Finance.Infrastructure.Persistence;

namespace UMS.Modules.Finance.Infrastructure.Persistence.Repositories;

internal sealed class FeeStructureRepository(FinanceDbContext context) : IFeeStructureRepository
{
    public Task<FeeStructure?> GetByIdAsync(FeeStructureId id, CancellationToken cancellationToken = default) =>
        context.FeeStructures.FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

    public Task<FeeStructure?> GetActiveAsync(string feeType, FeeApplicabilityType applicabilityType, Guid? applicabilityReferenceId, string? applicabilityServiceName, CancellationToken cancellationToken = default) =>
        context.FeeStructures.FirstOrDefaultAsync(
            f => f.FeeType == feeType
                && f.Status == FeeStructureStatus.Active
                && f.ApplicabilityType == applicabilityType
                && f.ApplicabilityReferenceId == applicabilityReferenceId
                && f.ApplicabilityServiceName == applicabilityServiceName,
            cancellationToken);

    public async Task<IReadOnlyList<FeeStructure>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await context.FeeStructures
            .OrderBy(f => f.FeeType).ThenByDescending(f => f.VersionNumber)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public void Add(FeeStructure feeStructure) => context.FeeStructures.Add(feeStructure);
}

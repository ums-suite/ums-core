using UMS.Modules.Finance.Domain.FeeStructures;

namespace UMS.Modules.Finance.Application.Abstractions;

public interface IFeeStructureRepository
{
    public Task<FeeStructure?> GetByIdAsync(FeeStructureId id, CancellationToken cancellationToken = default);

    /// <summary>The single Active row for a (FeeType, Applicability) pair, if any - FeeStructureConfiguration's own partial unique index guarantees at most one.</summary>
    public Task<FeeStructure?> GetActiveAsync(string feeType, FeeApplicabilityType applicabilityType, Guid? applicabilityReferenceId, string? applicabilityServiceName, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<FeeStructure>> GetAllAsync(CancellationToken cancellationToken = default);

    public void Add(FeeStructure feeStructure);
}

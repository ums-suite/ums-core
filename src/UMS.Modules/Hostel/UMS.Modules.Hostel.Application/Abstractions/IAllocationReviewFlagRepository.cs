using UMS.Modules.Hostel.Domain.Allocations;

namespace UMS.Modules.Hostel.Application.Abstractions;

/// <summary>HOS-17: the additive side-table repository (design-decisions.md "Student-Status-Change Review Flag").</summary>
public interface IAllocationReviewFlagRepository
{
    public Task<IReadOnlyList<AllocationReviewFlag>> GetByAllocationAsync(Guid allocationId, CancellationToken cancellationToken = default);

    public void Add(AllocationReviewFlag flag);
}

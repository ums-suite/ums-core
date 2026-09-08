using UMS.Modules.Hostel.Domain.Allocations;

namespace UMS.Modules.Hostel.Application.Abstractions;

public interface IAllocationRepository
{
    public Task<Allocation?> GetByIdAsync(AllocationId id, CancellationToken cancellationToken = default);

    public Task<Allocation?> GetByInvoiceIdAsync(Guid invoiceId, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<Allocation>> GetByStudentAsync(Guid studentId, CancellationToken cancellationToken = default);

    /// <summary>HOS-10: the grace-period sweep's candidate query - Pending Allocations whose grace deadline has passed.</summary>
    public Task<IReadOnlyList<Allocation>> GetPendingPastGraceDeadlineAsync(DateTimeOffset asOf, int batchSize, CancellationToken cancellationToken = default);

    public Task<bool> BedHasActiveOrPendingAllocationAsync(Guid bedId, CancellationToken cancellationToken = default);

    public Task<int> CountActiveByRoomAsync(Guid roomId, CancellationToken cancellationToken = default);

    public void Add(Allocation allocation);
}

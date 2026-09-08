using Microsoft.EntityFrameworkCore;
using UMS.Modules.Hostel.Application.Abstractions;
using UMS.Modules.Hostel.Domain.Allocations;

namespace UMS.Modules.Hostel.Infrastructure.Persistence.Repositories;

internal sealed class AllocationRepository(HostelDbContext context) : IAllocationRepository
{
    private static readonly AllocationStatus[] NonTerminalStatuses =
    [
        AllocationStatus.Pending,
        AllocationStatus.FeePaid,
        AllocationStatus.Active,
    ];

    public Task<Allocation?> GetByIdAsync(AllocationId id, CancellationToken cancellationToken = default) =>
        context.Allocations.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public Task<Allocation?> GetByInvoiceIdAsync(Guid invoiceId, CancellationToken cancellationToken = default) =>
        context.Allocations.FirstOrDefaultAsync(a => a.InvoiceId == invoiceId, cancellationToken);

    public async Task<IReadOnlyList<Allocation>> GetByStudentAsync(Guid studentId, CancellationToken cancellationToken = default) =>
        await context.Allocations.Where(a => a.StudentId == studentId).OrderByDescending(a => a.CreatedAt).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<Allocation>> GetPendingPastGraceDeadlineAsync(DateTimeOffset asOf, int batchSize, CancellationToken cancellationToken = default) =>
        await context.Allocations
            .Where(a => a.Status == AllocationStatus.Pending && a.FeeGraceDeadline < asOf)
            .OrderBy(a => a.FeeGraceDeadline)
            .Take(batchSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<bool> BedHasActiveOrPendingAllocationAsync(Guid bedId, CancellationToken cancellationToken = default) =>
        context.Allocations.AnyAsync(a => a.BedId == bedId && NonTerminalStatuses.Contains(a.Status), cancellationToken);

    /// <summary>
    /// requirement-spec.md §9 decision 5: counts every NON-TERMINAL Allocation in the Room (Pending/
    /// FeePaid/Active), not literally <c>Active</c> only - a Pending or FeePaid Allocation already
    /// holds its Bed, so it must count toward the capacity a Room-edit-time reduction is checked
    /// against, the same non-terminal-status reasoning <see cref="AllocationConfiguration"/>'s own
    /// partial unique indexes apply.
    /// </summary>
    public Task<int> CountActiveByRoomAsync(Guid roomId, CancellationToken cancellationToken = default) =>
        context.Allocations.CountAsync(a => a.RoomId == roomId && NonTerminalStatuses.Contains(a.Status), cancellationToken);

    public void Add(Allocation allocation) => context.Allocations.Add(allocation);
}

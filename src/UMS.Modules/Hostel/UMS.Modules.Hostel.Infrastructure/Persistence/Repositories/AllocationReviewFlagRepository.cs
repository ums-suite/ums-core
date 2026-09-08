using Microsoft.EntityFrameworkCore;
using UMS.Modules.Hostel.Application.Abstractions;
using UMS.Modules.Hostel.Domain.Allocations;

namespace UMS.Modules.Hostel.Infrastructure.Persistence.Repositories;

internal sealed class AllocationReviewFlagRepository(HostelDbContext context) : IAllocationReviewFlagRepository
{
    public async Task<IReadOnlyList<AllocationReviewFlag>> GetByAllocationAsync(Guid allocationId, CancellationToken cancellationToken = default) =>
        await context.AllocationReviewFlags.Where(f => f.AllocationId == allocationId).OrderByDescending(f => f.CreatedAt).ToListAsync(cancellationToken).ConfigureAwait(false);

    public void Add(AllocationReviewFlag flag) => context.AllocationReviewFlags.Add(flag);
}

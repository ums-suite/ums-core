using Microsoft.EntityFrameworkCore;
using UMS.Modules.Hostel.Application.Abstractions;
using UMS.Modules.Hostel.Domain.Complaints;

namespace UMS.Modules.Hostel.Infrastructure.Persistence.Repositories;

internal sealed class ComplaintRepository(HostelDbContext context) : IComplaintRepository
{
    public Task<Complaint?> GetByIdAsync(ComplaintId id, CancellationToken cancellationToken = default) =>
        context.Complaints.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Complaint>> GetByStudentAsync(Guid studentId, CancellationToken cancellationToken = default) =>
        await context.Complaints.Where(c => c.StudentId == studentId).OrderByDescending(c => c.CreatedAt).ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task<Complaint?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default) =>
        context.Complaints.FirstOrDefaultAsync(c => c.IdempotencyKey == idempotencyKey, cancellationToken);

    public Task<Complaint?> FindRecentDuplicateAsync(Guid studentId, Guid allocationId, string description, DateTimeOffset since, CancellationToken cancellationToken = default) =>
        context.Complaints.FirstOrDefaultAsync(
            c => c.StudentId == studentId && c.AllocationId == allocationId && c.Description == description && c.CreatedAt >= since,
            cancellationToken);

    public void Add(Complaint complaint) => context.Complaints.Add(complaint);
}

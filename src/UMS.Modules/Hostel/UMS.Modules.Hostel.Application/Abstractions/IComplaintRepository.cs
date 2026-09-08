using UMS.Modules.Hostel.Domain.Complaints;

namespace UMS.Modules.Hostel.Application.Abstractions;

public interface IComplaintRepository
{
    public Task<Complaint?> GetByIdAsync(ComplaintId id, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<Complaint>> GetByStudentAsync(Guid studentId, CancellationToken cancellationToken = default);

    public Task<Complaint?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>edge-cases.md "Duplicate complaint submission" fallback: the 60-second server-side dedupe window keyed on (student, allocation, description) for clients that omit an idempotency key.</summary>
    public Task<Complaint?> FindRecentDuplicateAsync(Guid studentId, Guid allocationId, string description, DateTimeOffset since, CancellationToken cancellationToken = default);

    public void Add(Complaint complaint);
}

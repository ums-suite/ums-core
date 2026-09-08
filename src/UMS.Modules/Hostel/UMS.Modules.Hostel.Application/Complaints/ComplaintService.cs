using UMS.Modules.Hostel.Application.Abstractions;
using UMS.Modules.Hostel.Application.Common;
using UMS.Modules.Hostel.Domain.Allocations;
using UMS.Modules.Hostel.Domain.Complaints;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Hostel.Application.Complaints;

/// <summary>
/// HOS-15/16: <c>POST /complaints</c>, <c>GET /complaints/me</c>, <c>PATCH /complaints/{id}</c>
/// (requirement-spec.md §2 Complaints, §6).
/// </summary>
public sealed class ComplaintService(
    IComplaintRepository complaints,
    IAllocationRepository allocations,
    HostelOptions options,
    IUnitOfWork unitOfWork,
    IAuditRecorder auditRecorder,
    IClock clock)
{
    /// <summary>requirement-spec.md §4: "A Complaint must reference an Allocation owned by the submitting Student"; §8 edge cases "grace-window complaint after checkout" and "duplicate submission deduplicated".</summary>
    public async Task<Result<ComplaintDto>> SubmitAsync(Guid studentId, SubmitComplaintRequest request, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<ComplaintCategory>(request.Category, ignoreCase: true, out var category))
        {
            return Error.Validation("complaint.invalid_category", $"'{request.Category}' is not a recognized Complaint category.");
        }

        var allocation = await allocations.GetByIdAsync(new AllocationId(request.AllocationId), cancellationToken).ConfigureAwait(false);
        if (allocation is null)
        {
            return Error.NotFound("allocation.not_found", $"No Allocation exists with id '{request.AllocationId}'.");
        }

        if (allocation.StudentId != studentId)
        {
            return Error.Forbidden("complaint.not_owner", "You do not own this Allocation.");
        }

        var now = clock.UtcNow;
        if (!IsWithinComplaintWindow(allocation, now))
        {
            return Error.Conflict("complaint.allocation_closed", $"Allocation '{allocation.Id}' was checked out more than {options.ComplaintPostCheckOutGraceDays} day(s) ago and can no longer receive new complaints.");
        }

        // design-decisions.md "Complaint Deduplication Mechanism": client-supplied idempotency key
        // as the primary mechanism, with a short server-side dedupe window as a best-effort fallback
        // (this is NOT a Money-and-Academic-Standing-criticality invariant - no DB constraint needed).
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var existingByKey = await complaints.GetByIdempotencyKeyAsync(request.IdempotencyKey, cancellationToken).ConfigureAwait(false);
            if (existingByKey is not null)
            {
                return ToDto(existingByKey);
            }
        }
        else
        {
            var since = now.AddSeconds(-options.ComplaintDedupeWindowSeconds);
            var duplicate = await complaints.FindRecentDuplicateAsync(studentId, request.AllocationId, request.Description, since, cancellationToken).ConfigureAwait(false);
            if (duplicate is not null)
            {
                return ToDto(duplicate);
            }
        }

        var created = Complaint.Submit(studentId, request.AllocationId, category, request.Description, request.IdempotencyKey, now);
        if (created.IsFailure)
        {
            return created.Error!;
        }

        complaints.Add(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(created.Value);
    }

    public async Task<IReadOnlyList<ComplaintDto>> GetByStudentAsync(Guid studentId, CancellationToken cancellationToken = default) =>
        (await complaints.GetByStudentAsync(studentId, cancellationToken).ConfigureAwait(false)).Select(ToDto).ToList();

    public async Task<Result<ComplaintDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var complaint = await complaints.GetByIdAsync(new ComplaintId(id), cancellationToken).ConfigureAwait(false);
        return complaint is null
            ? Error.NotFound("complaint.not_found", $"No Complaint exists with id '{id}'.")
            : ToDto(complaint);
    }

    /// <summary>HOS-16: Officer triage - <c>Open -&gt; InProgress -&gt; Resolved</c>/<c>Rejected</c>, with a resolution note (requirement-spec.md §2).</summary>
    public async Task<Result<ComplaintDto>> PatchAsync(Guid id, PatchComplaintRequest request, Guid officerUserId, string correlationId, CancellationToken cancellationToken = default)
    {
        var complaint = await complaints.GetByIdAsync(new ComplaintId(id), cancellationToken).ConfigureAwait(false);
        if (complaint is null)
        {
            return Error.NotFound("complaint.not_found", $"No Complaint exists with id '{id}'.");
        }

        var statusBefore = complaint.Status;
        var now = clock.UtcNow;

        var transitioned = request.Status.ToUpperInvariant() switch
        {
            "INPROGRESS" => complaint.StartProgress(),
            "RESOLVED" => complaint.Resolve(request.ResolutionNote ?? string.Empty, now),
            "REJECTED" => complaint.Reject(request.ResolutionNote ?? string.Empty, now),
            _ => Result.Failure(Error.Validation("complaint.invalid_status", $"'{request.Status}' is not a recognized Complaint status transition. Use InProgress, Resolved, or Rejected.")),
        };

        if (transitioned.IsFailure)
        {
            return transitioned.Error!;
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var audit = new AuditContext(officerUserId, ActorIpAddress: null, correlationId)
            .ToRequest("Complaint", complaint.Id.Value.ToString(), AuditActions.Update, $"{{\"status\":\"{statusBefore}\"}}", $"{{\"status\":\"{complaint.Status}\"}}", request.ResolutionNote);

        var committed = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, audit, cancellationToken).ConfigureAwait(false);
        return committed.IsFailure ? committed.Error! : ToDto(complaint);
    }

    internal static ComplaintDto ToDto(Complaint complaint) => new(
        complaint.Id.Value,
        complaint.StudentId,
        complaint.AllocationId,
        complaint.Category.ToString(),
        complaint.Description,
        complaint.Status.ToString(),
        complaint.ResolutionNote,
        complaint.CreatedAt,
        complaint.ResolvedAt);

    /// <summary>requirement-spec.md §8 edge case: a Complaint is allowed against a recently-ended Allocation within a short grace window (e.g. damage claims), read-only/closed after that.</summary>
    private bool IsWithinComplaintWindow(Allocation allocation, DateTimeOffset now) => allocation.Status switch
    {
        AllocationStatus.Active or AllocationStatus.FeePaid or AllocationStatus.Pending => true,
        AllocationStatus.CheckedOut => allocation.CheckedOutAt is not null && now <= allocation.CheckedOutAt.Value.AddDays(options.ComplaintPostCheckOutGraceDays),
        _ => false,
    };
}

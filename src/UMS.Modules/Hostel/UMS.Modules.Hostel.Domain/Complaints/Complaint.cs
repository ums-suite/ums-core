using UMS.Modules.Hostel.Domain.Common;
using UMS.Modules.Hostel.Domain.Events;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Hostel.Domain.Complaints;

/// <summary>
/// HOS-15/16: docs/ddd/ubiquitous-language.md - "a Student-raised issue against their Allocation."
/// Modeled as its own top-level, independently-queryable/repository-backed entity (own table, own
/// strongly-typed id) rather than a physically nested child of <c>Allocation</c> - the same pragmatic
/// choice Notifications' own <c>NotificationDeliveryAttempt</c> makes for a glossary-labeled "Entity"
/// that still needs its own independent query surface (<c>GET /complaints/me</c>,
/// <c>PATCH /complaints/{id}</c>).
///
/// <para>
/// requirement-spec.md §4: "A Complaint must reference an Allocation owned by the submitting
/// Student" - the ownership check itself is a cross-aggregate concern enforced by
/// <c>ComplaintService</c> (which loads the Allocation and compares its StudentId) before calling
/// <see cref="Submit"/>, not by this aggregate in isolation (it has no way to look up the
/// Allocation's owner itself).
/// </para>
/// </summary>
public sealed class Complaint : AggregateRoot<ComplaintId>
{
    private Complaint()
    {
    }

    private Complaint(ComplaintId id, Guid studentId, Guid allocationId, ComplaintCategory category, string description, string? idempotencyKey, DateTimeOffset now)
    {
        Id = id;
        StudentId = studentId;
        AllocationId = allocationId;
        Category = category;
        Description = description;
        IdempotencyKey = idempotencyKey;
        Status = ComplaintStatus.Open;
        CreatedAt = now;
    }

    public Guid StudentId { get; private set; }

    public Guid AllocationId { get; private set; }

    public ComplaintCategory Category { get; private set; }

    public string Description { get; private set; } = string.Empty;

    public ComplaintStatus Status { get; private set; }

    public string? ResolutionNote { get; private set; }

    public string? IdempotencyKey { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? ResolvedAt { get; private set; }

    public static Result<Complaint> Submit(Guid studentId, Guid allocationId, ComplaintCategory category, string description, string? idempotencyKey, DateTimeOffset now)
    {
        if (studentId == Guid.Empty || allocationId == Guid.Empty)
        {
            return Error.Validation("complaint.identifiers_required", "A Complaint requires both a valid studentId and allocationId.");
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            return Error.Validation("complaint.description_required", "A Complaint's description is required.");
        }

        var complaint = new Complaint(ComplaintId.New(), studentId, allocationId, category, description.Trim(), idempotencyKey, now);
        complaint.Raise(new ComplaintSubmitted(complaint.Id.Value, studentId, allocationId, now));
        return complaint;
    }

    public Result StartProgress()
    {
        if (Status != ComplaintStatus.Open)
        {
            return Result.Failure(Error.Conflict("complaint.not_open", $"Complaint '{Id}' cannot start progress - it is currently '{Status}' (requires 'Open')."));
        }

        Status = ComplaintStatus.InProgress;
        return Result.Success();
    }

    public Result Resolve(string resolutionNote, DateTimeOffset now)
    {
        var mutability = EnsureTriageable();
        if (mutability.IsFailure)
        {
            return mutability;
        }

        if (string.IsNullOrWhiteSpace(resolutionNote))
        {
            return Result.Failure(Error.Validation("complaint.resolution_note_required", "A resolution requires a note."));
        }

        Status = ComplaintStatus.Resolved;
        ResolutionNote = resolutionNote.Trim();
        ResolvedAt = now;
        Raise(new ComplaintResolved(Id.Value, StudentId, AllocationId, ResolutionNote, now));
        return Result.Success();
    }

    public Result Reject(string resolutionNote, DateTimeOffset now)
    {
        var mutability = EnsureTriageable();
        if (mutability.IsFailure)
        {
            return mutability;
        }

        if (string.IsNullOrWhiteSpace(resolutionNote))
        {
            return Result.Failure(Error.Validation("complaint.resolution_note_required", "A rejection requires a note."));
        }

        Status = ComplaintStatus.Rejected;
        ResolutionNote = resolutionNote.Trim();
        ResolvedAt = now;
        Raise(new ComplaintResolved(Id.Value, StudentId, AllocationId, $"Rejected: {ResolutionNote}", now));
        return Result.Success();
    }

    private Result EnsureTriageable() =>
        Status is ComplaintStatus.Open or ComplaintStatus.InProgress
            ? Result.Success()
            : Result.Failure(Error.Conflict("complaint.already_closed", $"Complaint '{Id}' is already '{Status}'."));
}

using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Hostel.Domain.Allocations;

/// <summary>
/// HOS-17: design-decisions.md "Student-Status-Change Review Flag - Additive Side-Table, Not an
/// Allocation Field". Deliberately NOT a field on <see cref="Allocation"/> and deliberately NOT an
/// <see cref="Common.AggregateRoot{TId}"/> with its own state machine - a plain, insert-only record
/// keyed on <see cref="AllocationId"/>, so writing it can never contend with the Allocation's own
/// state-machine writes (edge-cases.md "Student status changes to Suspended/Graduated mid-allocation,
/// racing officer review"). Advisory only: it must never itself gate or force a transition -
/// <c>AllocationService</c> never reads this table before allowing a transition, only an Officer's
/// own UI does, to prompt a human decision.
/// </summary>
public sealed class AllocationReviewFlag
{
    private AllocationReviewFlag()
    {
    }

    private AllocationReviewFlag(Guid id, Guid allocationId, string reason, string sourceEventReference, DateTimeOffset now)
    {
        Id = id;
        AllocationId = allocationId;
        Reason = reason;
        SourceEventReference = sourceEventReference;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid AllocationId { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    public string SourceEventReference { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public static Result<AllocationReviewFlag> Create(Guid allocationId, string reason, string sourceEventReference, DateTimeOffset now)
    {
        if (allocationId == Guid.Empty)
        {
            return Error.Validation("allocation_review_flag.allocation_id_required", "An AllocationReviewFlag requires a non-empty allocationId.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Error.Validation("allocation_review_flag.reason_required", "An AllocationReviewFlag's reason is required.");
        }

        return new AllocationReviewFlag(Guid.NewGuid(), allocationId, reason.Trim(), sourceEventReference, now);
    }
}

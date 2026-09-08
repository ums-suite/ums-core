using UMS.Modules.Hostel.Domain.Common;
using UMS.Modules.Hostel.Domain.Events;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Hostel.Domain.Allocations;

/// <summary>
/// HOS-7..14: docs/ddd/ubiquitous-language.md - "the binding of exactly one Student to exactly one
/// Bed, enforced by a DB constraint, not just application logic." requirement-spec.md §3:
/// <c>Pending -&gt; FeePaid -&gt; Active -&gt; CheckedOut</c> (plus <see cref="AllocationStatus.Expired"/>).
///
/// <para>
/// <b>Concurrency mechanism - read this before changing any writer of this aggregate.</b>
/// design-decisions.md "Bed-Allocation Concurrency Control Pattern": every writer that changes a
/// Bed's occupancy state - <see cref="Create"/> (allocation creation) AND <see cref="CheckOut"/>
/// (bed-freeing) - is only ever called by <c>AllocationService</c> after it has taken a pessimistic
/// <c>SELECT ... FOR UPDATE</c> lock on the target <c>Bed</c> row (mirroring Finance's own
/// <c>Payment</c>/<c>Invoice</c> lock pattern), with the partial unique indexes
/// (<c>UNIQUE (bed_id) WHERE status = 'Active'</c>, <c>UNIQUE (student_id) WHERE status = 'Active'</c>
/// on <c>hostel.allocations</c>) retained as the DB-level backstop - defense-in-depth, never
/// either/or (requirement-spec.md §4).
/// </para>
/// </summary>
public sealed class Allocation : AggregateRoot<AllocationId>
{
    private Allocation()
    {
    }

    private Allocation(AllocationId id, Guid studentId, Guid bedId, Guid roomId, Guid hostelId, Guid hostelApplicationId, DateTimeOffset feeGraceDeadline, DateTimeOffset now)
    {
        Id = id;
        StudentId = studentId;
        BedId = bedId;
        RoomId = roomId;
        HostelId = hostelId;
        HostelApplicationId = hostelApplicationId;
        FeeGraceDeadline = feeGraceDeadline;
        Status = AllocationStatus.Pending;
        CreatedAt = now;
    }

    public Guid StudentId { get; private set; }

    public Guid BedId { get; private set; }

    public Guid RoomId { get; private set; }

    public Guid HostelId { get; private set; }

    public Guid HostelApplicationId { get; private set; }

    public AllocationStatus Status { get; private set; }

    public Guid? InvoiceId { get; private set; }

    public DateTimeOffset FeeGraceDeadline { get; private set; }

    public CheckOutType? CheckOutKind { get; private set; }

    public bool RefundRequested { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? FeePaidAt { get; private set; }

    public DateTimeOffset? ActivatedAt { get; private set; }

    public DateTimeOffset? CheckedOutAt { get; private set; }

    public DateTimeOffset? ExpiredAt { get; private set; }

    /// <summary>HOS-7: called only after the caller has taken the target Bed's row lock and confirmed no other Active/Pending Allocation already claims it (see class remarks).</summary>
    public static Result<Allocation> Create(Guid studentId, Guid bedId, Guid roomId, Guid hostelId, Guid hostelApplicationId, DateTimeOffset feeGraceDeadline, DateTimeOffset now)
    {
        if (studentId == Guid.Empty || bedId == Guid.Empty || roomId == Guid.Empty || hostelId == Guid.Empty || hostelApplicationId == Guid.Empty)
        {
            return Error.Validation("allocation.identifiers_required", "An Allocation requires a valid studentId, bedId, roomId, hostelId, and hostelApplicationId.");
        }

        var allocation = new Allocation(AllocationId.New(), studentId, bedId, roomId, hostelId, hostelApplicationId, feeGraceDeadline, now);
        allocation.Raise(new BedAllocated(allocation.Id.Value, studentId, bedId, roomId, hostelId, hostelApplicationId, now));
        return allocation;
    }

    /// <summary>HOS-8: recorded once Finance's CreateInvoice call for the hostel fee returns (requirement-spec.md §2 step 7).</summary>
    public void RecordInvoice(Guid invoiceId) => InvoiceId = invoiceId;

    /// <summary>HOS-9: Finance's PaymentSucceeded, filtered to this Allocation's own invoice (requirement-spec.md §2 step 8). Idempotent - a replayed event against an already-FeePaid/later Allocation is a no-op.</summary>
    public Result MarkFeePaid(DateTimeOffset now)
    {
        if (Status != AllocationStatus.Pending)
        {
            return Status is AllocationStatus.FeePaid or AllocationStatus.Active
                ? Result.Success()
                : Result.Failure(Error.Conflict("allocation.not_pending", $"Allocation '{Id}' cannot be marked fee-paid - it is currently '{Status}'."));
        }

        Status = AllocationStatus.FeePaid;
        FeePaidAt = now;
        return Result.Success();
    }

    /// <summary>HOS-11: requirement-spec.md §4 - "cannot reach Active before FeePaid, no manual override without an audited officer reason" (the audited-override path, if ever needed, is a Money-and-Academic-Standing-criticality decision left to a future ticket - not built here since no requirement names its concrete shape).</summary>
    public Result CheckIn(DateTimeOffset now)
    {
        if (Status != AllocationStatus.FeePaid)
        {
            return Result.Failure(Error.Conflict("allocation.not_fee_paid", $"Allocation '{Id}' cannot be checked in - it is currently '{Status}' (requires 'FeePaid')."));
        }

        Status = AllocationStatus.Active;
        ActivatedAt = now;
        Raise(new AllocationActivated(Id.Value, StudentId, now));
        return Result.Success();
    }

    /// <summary>HOS-13: called only after the caller has taken the target Bed's row lock (see class remarks - symmetric with <see cref="Create"/>). requirement-spec.md §2 Check-out: "Voluntary early check-out triggers a refund request to Finance."</summary>
    public Result CheckOut(CheckOutType checkOutType, DateTimeOffset now)
    {
        if (Status is not (AllocationStatus.Pending or AllocationStatus.FeePaid or AllocationStatus.Active))
        {
            return Result.Failure(Error.Conflict("allocation.not_checkoutable", $"Allocation '{Id}' cannot be checked out - it is currently '{Status}'."));
        }

        var refundEligible = checkOutType == Allocations.CheckOutType.Voluntary && Status == AllocationStatus.Active;

        Status = AllocationStatus.CheckedOut;
        CheckOutKind = checkOutType;
        CheckedOutAt = now;
        RefundRequested = refundEligible;
        Raise(new AllocationCheckedOut(Id.Value, StudentId, BedId, RoomId, checkOutType.ToString(), refundEligible, now));
        return Result.Success();
    }

    /// <summary>HOS-10: the fee-grace-period auto-expiry sweep - only ever a Pending Allocation past <see cref="FeeGraceDeadline"/> (edge-cases.md "Fee-payment grace period expiring while payment is in flight").</summary>
    public Result Expire(DateTimeOffset now)
    {
        if (Status != AllocationStatus.Pending)
        {
            return Result.Failure(Error.Conflict("allocation.not_pending", $"Allocation '{Id}' cannot expire - it is currently '{Status}' (requires 'Pending')."));
        }

        Status = AllocationStatus.Expired;
        ExpiredAt = now;
        Raise(new AllocationExpired(Id.Value, StudentId, BedId, RoomId, now));
        return Result.Success();
    }
}

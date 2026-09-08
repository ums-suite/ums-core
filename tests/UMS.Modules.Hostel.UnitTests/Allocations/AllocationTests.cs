using UMS.Modules.Hostel.Domain.Allocations;
using UMS.Modules.Hostel.Domain.Events;

namespace UMS.Modules.Hostel.UnitTests.Allocations;

/// <summary>HOS-7..13: requirement-spec.md §3/§4; design-decisions.md's Bed-allocation-concurrency-pattern remarks apply to the caller (repository lock), not this aggregate - these tests cover only the in-memory state machine.</summary>
public sealed class AllocationTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static Allocation Pending() =>
        Allocation.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Now.AddDays(7), Now).Value;

    [Fact]
    public void Create_raises_BedAllocated_in_Pending_status()
    {
        var allocation = Pending();

        Assert.Equal(AllocationStatus.Pending, allocation.Status);
        Assert.Contains(allocation.DomainEvents, e => e is BedAllocated);
    }

    [Fact]
    public void CheckIn_before_FeePaid_is_rejected()
    {
        var allocation = Pending();

        var result = allocation.CheckIn(Now);

        Assert.True(result.IsFailure);
        Assert.Equal("allocation.not_fee_paid", result.Error!.Code);
    }

    [Fact]
    public void CheckIn_after_FeePaid_succeeds_and_raises_AllocationActivated()
    {
        var allocation = Pending();
        allocation.MarkFeePaid(Now);

        var result = allocation.CheckIn(Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(AllocationStatus.Active, allocation.Status);
        Assert.Contains(allocation.DomainEvents, e => e is AllocationActivated);
    }

    [Fact]
    public void Voluntary_checkout_from_Active_is_refund_eligible()
    {
        var allocation = Pending();
        allocation.MarkFeePaid(Now);
        allocation.CheckIn(Now);

        var result = allocation.CheckOut(CheckOutType.Voluntary, Now);

        Assert.True(result.IsSuccess);
        Assert.True(allocation.RefundRequested);
        Assert.Equal(AllocationStatus.CheckedOut, allocation.Status);
        Assert.Contains(allocation.DomainEvents, e => e is AllocationCheckedOut { RefundEligible: true });
    }

    [Fact]
    public void EndOfSession_checkout_is_never_refund_eligible()
    {
        var allocation = Pending();
        allocation.MarkFeePaid(Now);
        allocation.CheckIn(Now);

        allocation.CheckOut(CheckOutType.EndOfSession, Now);

        Assert.False(allocation.RefundRequested);
    }

    [Fact]
    public void Voluntary_checkout_from_Pending_before_check_in_is_not_refund_eligible()
    {
        var allocation = Pending();

        var result = allocation.CheckOut(CheckOutType.Voluntary, Now);

        Assert.True(result.IsSuccess);
        Assert.False(allocation.RefundRequested);
    }

    [Fact]
    public void Checkout_is_terminal_requirement_spec_once_CheckedOut_an_Allocation_is_terminal()
    {
        var allocation = Pending();
        allocation.CheckOut(CheckOutType.Voluntary, Now);

        var result = allocation.CheckOut(CheckOutType.EndOfSession, Now);

        Assert.True(result.IsFailure);
        Assert.Equal("allocation.not_checkoutable", result.Error!.Code);
    }

    [Fact]
    public void Expire_from_Pending_succeeds_and_raises_AllocationExpired()
    {
        var allocation = Pending();

        var result = allocation.Expire(Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(AllocationStatus.Expired, allocation.Status);
        Assert.Contains(allocation.DomainEvents, e => e is AllocationExpired);
    }

    [Fact]
    public void Expire_after_FeePaid_is_rejected_edge_case_late_payment_races_the_sweep()
    {
        var allocation = Pending();
        allocation.MarkFeePaid(Now);

        var result = allocation.Expire(Now);

        Assert.True(result.IsFailure);
        Assert.Equal("allocation.not_pending", result.Error!.Code);
    }
}

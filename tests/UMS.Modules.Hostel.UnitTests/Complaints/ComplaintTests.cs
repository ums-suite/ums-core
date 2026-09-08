using UMS.Modules.Hostel.Domain.Complaints;
using UMS.Modules.Hostel.Domain.Events;

namespace UMS.Modules.Hostel.UnitTests.Complaints;

/// <summary>HOS-15/16: requirement-spec.md §2 Complaints, §4.</summary>
public sealed class ComplaintTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static Complaint Open() =>
        Complaint.Submit(Guid.NewGuid(), Guid.NewGuid(), ComplaintCategory.Maintenance, "The fan is broken.", idempotencyKey: null, Now).Value;

    [Fact]
    public void Submit_without_a_description_is_rejected()
    {
        var result = Complaint.Submit(Guid.NewGuid(), Guid.NewGuid(), ComplaintCategory.Maintenance, "  ", null, Now);

        Assert.True(result.IsFailure);
        Assert.Equal("complaint.description_required", result.Error!.Code);
    }

    [Fact]
    public void Submit_raises_ComplaintSubmitted_in_Open_status()
    {
        var complaint = Open();

        Assert.Equal(ComplaintStatus.Open, complaint.Status);
        Assert.Contains(complaint.DomainEvents, e => e is ComplaintSubmitted);
    }

    [Fact]
    public void Resolve_requires_a_note()
    {
        var complaint = Open();

        var result = complaint.Resolve(string.Empty, Now);

        Assert.True(result.IsFailure);
        Assert.Equal("complaint.resolution_note_required", result.Error!.Code);
    }

    [Fact]
    public void Resolve_from_Open_succeeds_and_raises_ComplaintResolved()
    {
        var complaint = Open();

        var result = complaint.Resolve("Fixed the fan.", Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(ComplaintStatus.Resolved, complaint.Status);
        Assert.Contains(complaint.DomainEvents, e => e is ComplaintResolved);
    }

    [Fact]
    public void Resolve_from_InProgress_succeeds()
    {
        var complaint = Open();
        complaint.StartProgress();

        var result = complaint.Resolve("Fixed.", Now);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Resolve_an_already_closed_Complaint_is_rejected()
    {
        var complaint = Open();
        complaint.Resolve("Fixed.", Now);

        var result = complaint.Resolve("Fixed again.", Now);

        Assert.True(result.IsFailure);
        Assert.Equal("complaint.already_closed", result.Error!.Code);
    }

    [Fact]
    public void Reject_from_Open_succeeds()
    {
        var complaint = Open();

        var result = complaint.Reject("Not a hostel issue.", Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(ComplaintStatus.Rejected, complaint.Status);
    }
}

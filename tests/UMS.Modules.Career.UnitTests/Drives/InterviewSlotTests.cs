using UMS.Modules.Career.Domain.Drives;

namespace UMS.Modules.Career.UnitTests.Drives;

/// <summary>design-decisions.md "Interview-Slot Booking Concurrency Control" - the domain-level guards that back the DB-level check constraint; the atomic conditional UPDATE itself is exercised by the integration suite's genuine concurrency test.</summary>
public sealed class InterviewSlotTests
{
    [Fact]
    public void Create_throws_when_end_time_is_not_after_start_time()
    {
        var start = DateTimeOffset.UtcNow;
        Assert.Throws<ArgumentException>(() => InterviewSlot.Create(CampusRecruitmentDriveId.New(), start, start, capacity: 1));
    }

    [Fact]
    public void Create_throws_when_capacity_is_less_than_one()
    {
        var start = DateTimeOffset.UtcNow;
        Assert.Throws<ArgumentException>(() => InterviewSlot.Create(CampusRecruitmentDriveId.New(), start, start.AddMinutes(30), capacity: 0));
    }

    [Fact]
    public void A_freshly_created_slot_has_capacity_and_is_not_cancelled()
    {
        var start = DateTimeOffset.UtcNow;
        var slot = InterviewSlot.Create(CampusRecruitmentDriveId.New(), start, start.AddMinutes(30), capacity: 1);

        Assert.True(slot.HasCapacity());
        Assert.False(slot.IsCancelled);
        Assert.Equal(0, slot.BookedCount);
    }

    [Fact]
    public void MarkCancelled_makes_a_slot_report_no_capacity_even_if_unbooked()
    {
        var start = DateTimeOffset.UtcNow;
        var slot = InterviewSlot.Create(CampusRecruitmentDriveId.New(), start, start.AddMinutes(30), capacity: 1);

        slot.MarkCancelled();

        Assert.False(slot.HasCapacity());
    }
}

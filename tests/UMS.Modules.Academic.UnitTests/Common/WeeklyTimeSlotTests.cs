using UMS.Modules.Academic.Domain.Common;

namespace UMS.Modules.Academic.UnitTests.Common;

/// <summary>requirement-spec.md §4 timetable-conflict-gate invariant - the exact comparison unit ACD-6 relies on.</summary>
public sealed class WeeklyTimeSlotTests
{
    [Fact]
    public void OverlapsWith_is_true_for_the_same_day_with_intersecting_times()
    {
        var a = WeeklyTimeSlot.Create(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(11, 0)).Value;
        var b = WeeklyTimeSlot.Create(DayOfWeek.Monday, new TimeOnly(10, 0), new TimeOnly(12, 0)).Value;

        Assert.True(a.OverlapsWith(b));
        Assert.True(b.OverlapsWith(a));
    }

    [Fact]
    public void OverlapsWith_is_false_for_different_days_even_with_identical_times()
    {
        var a = WeeklyTimeSlot.Create(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(11, 0)).Value;
        var b = WeeklyTimeSlot.Create(DayOfWeek.Tuesday, new TimeOnly(9, 0), new TimeOnly(11, 0)).Value;

        Assert.False(a.OverlapsWith(b));
    }

    [Fact]
    public void OverlapsWith_is_false_for_the_same_day_with_back_to_back_non_overlapping_times()
    {
        var a = WeeklyTimeSlot.Create(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(10, 0)).Value;
        var b = WeeklyTimeSlot.Create(DayOfWeek.Monday, new TimeOnly(10, 0), new TimeOnly(11, 0)).Value;

        Assert.False(a.OverlapsWith(b));
    }

    [Fact]
    public void Create_rejects_an_end_time_at_or_before_the_start_time()
    {
        Assert.True(WeeklyTimeSlot.Create(DayOfWeek.Monday, new TimeOnly(10, 0), new TimeOnly(10, 0)).IsFailure);
        Assert.True(WeeklyTimeSlot.Create(DayOfWeek.Monday, new TimeOnly(11, 0), new TimeOnly(10, 0)).IsFailure);
    }
}

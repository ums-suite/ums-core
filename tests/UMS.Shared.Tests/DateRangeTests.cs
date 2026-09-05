using UMS.Shared.Domain;

namespace UMS.Shared.Tests;

public sealed class DateRangeTests
{
    [Fact]
    public void Create_rejects_an_end_date_before_the_start_date()
    {
        var result = DateRange.Create(new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 1));

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Contains_is_inclusive_of_both_bounds()
    {
        var range = DateRange.Create(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31)).Value;

        Assert.True(range.Contains(new DateOnly(2026, 1, 1)));
        Assert.True(range.Contains(new DateOnly(2026, 1, 31)));
        Assert.False(range.Contains(new DateOnly(2025, 12, 31)));
        Assert.False(range.Contains(new DateOnly(2026, 2, 1)));
    }

    [Fact]
    public void OverlapsWith_detects_intersecting_ranges()
    {
        var a = DateRange.Create(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 15)).Value;
        var b = DateRange.Create(new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 20)).Value;
        var c = DateRange.Create(new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 15)).Value;

        Assert.True(a.OverlapsWith(b));
        Assert.False(a.OverlapsWith(c));
    }
}

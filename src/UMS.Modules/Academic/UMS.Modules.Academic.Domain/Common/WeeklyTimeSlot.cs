using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Academic.Domain.Common;

/// <summary>
/// A recurring weekly class-meeting slot (day of week + time-of-day range) - the timetable-conflict
/// gate's own comparison unit (requirement-spec.md academic §2 Semester Registration, §4
/// timetable-conflict-gate invariant). Module-local - no shared platform-wide equivalent exists;
/// distinct from <c>UMS.Shared.Domain.DateRange</c>, which models a calendar date range, not a
/// recurring weekly time-of-day slot.
/// </summary>
public sealed record WeeklyTimeSlot
{
    private WeeklyTimeSlot(DayOfWeek dayOfWeek, TimeOnly start, TimeOnly end)
    {
        DayOfWeek = dayOfWeek;
        Start = start;
        End = end;
    }

    public DayOfWeek DayOfWeek { get; }

    public TimeOnly Start { get; }

    public TimeOnly End { get; }

    public static Result<WeeklyTimeSlot> Create(DayOfWeek dayOfWeek, TimeOnly start, TimeOnly end) =>
        end <= start
            ? Error.Validation("weekly_time_slot.invalid", "A WeeklyTimeSlot's end time must be after its start time.")
            : new WeeklyTimeSlot(dayOfWeek, start, end);

    /// <summary>requirement-spec.md §4 timetable-conflict-gate: true if this slot and <paramref name="other"/> fall on the same day and their time ranges intersect.</summary>
    public bool OverlapsWith(WeeklyTimeSlot other) =>
        DayOfWeek == other.DayOfWeek && Start < other.End && other.Start < End;
}

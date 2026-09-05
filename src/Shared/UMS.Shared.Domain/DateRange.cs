using UMS.Shared.ErrorHandling.Results;

namespace UMS.Shared.Domain;

/// <summary>
/// A start/end date range with its own overlap-check behavior (ums-conventions.md, Domain
/// Modeling: "DateRange (start/end with its own overlap-check behavior)"). Scaffolded here as
/// part of Academic (release/DEVELOPMENT_PLAN.md Flow #12; requirement-spec.md academic §2 -
/// a Semester's registration window, an Enrollment's drop window) - the first module needing a
/// reusable date-range concept; every later module (Admission's application window, Finance's
/// fee-due window) reuses this instead of a pair of bare <c>DateOnly</c> fields re-implementing
/// the same overlap/containment checks.
/// </summary>
public sealed record DateRange
{
    private DateRange(DateOnly start, DateOnly end)
    {
        Start = start;
        End = end;
    }

    public DateOnly Start { get; }

    public DateOnly End { get; }

    public static Result<DateRange> Create(DateOnly start, DateOnly end) =>
        end < start
            ? Error.Validation("date_range.invalid", "A DateRange's end date must not be before its start date.")
            : new DateRange(start, end);

    /// <summary>True if <paramref name="date"/> falls within this range, inclusive of both bounds.</summary>
    public bool Contains(DateOnly date) => date >= Start && date <= End;

    /// <summary>True if <paramref name="instant"/>'s date falls within this range, inclusive of both bounds - the shape a "is registration/drop window still open" check needs against <see cref="DateTimeOffset.UtcNow"/>.</summary>
    public bool Contains(DateTimeOffset instant) => Contains(DateOnly.FromDateTime(instant.UtcDateTime));

    public bool OverlapsWith(DateRange other) => Start <= other.End && other.Start <= End;
}

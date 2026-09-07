using UMS.Modules.Academic.Domain.Common;

namespace UMS.Modules.Academic.Domain.CourseOfferings;

/// <summary>One parallel meeting-time group within a CourseOffering (docs/ddd/ubiquitous-language.md: "CourseOffering ... with Sections"). The timetable-conflict gate (requirement-spec.md §4) compares each Enrollment's chosen Section's <see cref="Schedule"/> against a Student's other Active enrollments.</summary>
public sealed class Section
{
    internal Section(SectionId id, CourseOfferingId courseOfferingId, string code, WeeklyTimeSlot schedule)
    {
        Id = id;
        CourseOfferingId = courseOfferingId;
        Code = code;
        Schedule = schedule;
    }

    private Section()
    {
    }

    public SectionId Id { get; private set; }

    public CourseOfferingId CourseOfferingId { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public WeeklyTimeSlot Schedule { get; private set; } = null!;
}

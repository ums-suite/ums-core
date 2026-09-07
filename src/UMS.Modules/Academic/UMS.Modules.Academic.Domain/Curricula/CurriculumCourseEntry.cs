using UMS.Modules.Academic.Domain.Courses;

namespace UMS.Modules.Academic.Domain.Curricula;

/// <summary>One Course's membership in a Curriculum version - required or elective (requirement-spec.md §2).</summary>
public sealed class CurriculumCourseEntry
{
    internal CurriculumCourseEntry(CurriculumId curriculumId, CourseId courseId, bool isRequired)
    {
        CurriculumId = curriculumId;
        CourseId = courseId;
        IsRequired = isRequired;
    }

    private CurriculumCourseEntry()
    {
    }

    public CurriculumId CurriculumId { get; private set; }

    public CourseId CourseId { get; private set; }

    public bool IsRequired { get; private set; }
}

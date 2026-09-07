namespace UMS.Modules.Academic.Domain.Courses;

/// <summary>One declared prerequisite edge: <see cref="CourseId"/> requires a passing Grade in <see cref="PrerequisiteCourseId"/> (requirement-spec.md §2, §4 prerequisite-gate invariant).</summary>
public sealed class CoursePrerequisite
{
    internal CoursePrerequisite(CourseId courseId, CourseId prerequisiteCourseId)
    {
        CourseId = courseId;
        PrerequisiteCourseId = prerequisiteCourseId;
    }

    private CoursePrerequisite()
    {
    }

    public CourseId CourseId { get; private set; }

    public CourseId PrerequisiteCourseId { get; private set; }
}

using UMS.Modules.Academic.Domain.Common;

namespace UMS.Modules.Academic.Domain.Courses;

/// <summary>
/// ACD-2: a catalog-level Course definition (code/title/credit), independent of when it is taught
/// (requirement-spec.md §2 Curriculum &amp; Course Catalog Management, §3). Prerequisites are
/// declared here, at the Course level, and evaluated at enrollment time by ACD-6 (§2) - NOT via
/// any `Curriculum` version, which requirement-spec.md §2/§9 decision 6 keeps a purely
/// administrative/degree-audit concept for this v1 pass.
/// </summary>
public sealed class Course : AggregateRoot<CourseId>
{
    private readonly List<CoursePrerequisite> _prerequisites = [];

    private Course()
    {
    }

    private Course(CourseId id, string code, string title, CreditHours creditHours, DateTimeOffset now)
    {
        Id = id;
        Code = code;
        Title = title;
        CreditHours = creditHours;
        CreatedAt = now;
    }

    public string Code { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public CreditHours CreditHours { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyCollection<CoursePrerequisite> Prerequisites => _prerequisites.AsReadOnly();

    public static Course Create(string code, string title, CreditHours creditHours, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Course code is required.", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Course title is required.", nameof(title));
        }

        return new Course(CourseId.New(), code.Trim(), title.Trim(), creditHours, now);
    }

    /// <summary>ACD-2: declares that this Course requires a passing Grade in <paramref name="prerequisiteCourseId"/> before enrollment (requirement-spec.md §4 prerequisite-gate).</summary>
    public void AddPrerequisite(CourseId prerequisiteCourseId)
    {
        if (prerequisiteCourseId == Id)
        {
            throw new InvalidOperationException("A Course cannot be its own prerequisite.");
        }

        if (_prerequisites.Any(p => p.PrerequisiteCourseId == prerequisiteCourseId))
        {
            return;
        }

        _prerequisites.Add(new CoursePrerequisite(Id, prerequisiteCourseId));
    }
}

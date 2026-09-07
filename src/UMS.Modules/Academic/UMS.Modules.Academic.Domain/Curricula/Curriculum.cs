using UMS.Modules.Academic.Domain.Common;
using UMS.Modules.Academic.Domain.Courses;

namespace UMS.Modules.Academic.Domain.Curricula;

/// <summary>
/// ACD-1: a versioned set of required/elective Courses for a Program (requirement-spec.md §2
/// Curriculum &amp; Course Catalog Management, §3, §9 decision 6). A version change never
/// retroactively moves an already-admitted cohort - this v1 pass models Curriculum purely as an
/// administrative/degree-audit catalog (creatable and versionable); which specific Curriculum
/// version applies to which already-admitted Student is Admission/Student's own concern (not
/// resolved here, and not consulted by ACD-6's prerequisite gate, which evaluates prerequisites
/// directly off `Course.Prerequisites` per requirement-spec.md §2's own explicit statement).
/// </summary>
public sealed class Curriculum : AggregateRoot<CurriculumId>
{
    private readonly List<CurriculumCourseEntry> _courses = [];

    private Curriculum()
    {
    }

    private Curriculum(CurriculumId id, Guid programId, int version, DateTimeOffset now)
    {
        Id = id;
        ProgramId = programId;
        CurriculumVersion = version;
        CreatedAt = now;
    }

    public Guid ProgramId { get; private set; }

    /// <summary>The Curriculum's own business-meaning version number (requirement-spec.md §2/§9 decision 6) - deliberately a distinct property from <see cref="AggregateRoot{TId}.Version"/> (that one is the unrelated `xmin` optimistic-concurrency token every aggregate carries; conflating the two would let a routine concurrent metadata edit silently bump the business-meaning curriculum version).</summary>
    public int CurriculumVersion { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyCollection<CurriculumCourseEntry> Courses => _courses.AsReadOnly();

    public static Curriculum Create(Guid programId, int version, DateTimeOffset now)
    {
        if (version < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(version), "Curriculum version must be at least 1.");
        }

        return new Curriculum(CurriculumId.New(), programId, version, now);
    }

    public void AddCourse(CourseId courseId, bool isRequired)
    {
        if (_courses.Any(c => c.CourseId == courseId))
        {
            return;
        }

        _courses.Add(new CurriculumCourseEntry(Id, courseId, isRequired));
    }
}

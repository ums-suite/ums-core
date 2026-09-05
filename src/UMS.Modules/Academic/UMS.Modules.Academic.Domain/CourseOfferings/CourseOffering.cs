using UMS.Modules.Academic.Domain.Common;
using UMS.Modules.Academic.Domain.Events;

namespace UMS.Modules.Academic.Domain.CourseOfferings;

/// <summary>
/// ACD-3: one Course actually scheduled in one Semester, with Sections and an Instructor
/// (docs/ddd/ubiquitous-language.md). Owns seat-limit state (<see cref="EnrolledCount"/>/
/// <see cref="Capacity"/>) - the aggregate this document's own concurrency-invariant is about.
///
/// <para>
/// <b>Important:</b> <see cref="EnrolledCount"/> is NEVER mutated through this aggregate's own
/// in-memory setters/SaveChanges path in ACD-6/ACD-7's actual enrollment/drop flow -
/// design-decisions.md's "Seat-Limit Concurrency Control Pattern" mandates an atomic conditional
/// <c>UPDATE ... WHERE enrolled_count &lt; capacity</c> issued directly by the repository
/// (<c>CourseOfferingRepository.TryIncrementEnrolledCountAsync</c>/
/// <c>TryDecrementEnrolledCountAsync</c>), bypassing EF's change tracker entirely so the
/// conditional <c>WHERE</c> clause and the increment happen as one indivisible statement Postgres
/// itself serializes - not a version column, not a held lock. The field here exists for reads
/// (capacity displays, seat-availability queries) and for this aggregate's own invariants that
/// don't need that race-proof guarantee (e.g. construction-time validation).
/// </para>
///
/// <para>
/// <see cref="InstructorFacultyMemberId"/> is Academic's own authoritative copy of the
/// Faculty↔CourseOffering assignment (docs/ddd/ubiquitous-language.md, `CourseAssignment`:
/// "Written by Academic ... Faculty holds an eventually-consistent projection of it") -
/// <see cref="AssignInstructor"/>/<see cref="UnassignInstructor"/> raise
/// <see cref="Events.InstructorAssigned"/>/<see cref="Events.InstructorUnassigned"/>, which
/// Faculty's own FAC-4 outbox relay (already merged) consumes to maintain its own projection.
/// There is no separate `CourseAssignment` aggregate/table in this module - the CourseOffering
/// itself is the natural aggregate boundary for "which Instructor teaches this offering"
/// (docs/ddd/ubiquitous-language.md's own CourseOffering definition already names "an Instructor"
/// as part of what a CourseOffering owns).
/// </para>
/// </summary>
public sealed class CourseOffering : AggregateRoot<CourseOfferingId>
{
    private readonly List<Section> _sections = [];
    private readonly List<Exam> _exams = [];

    private CourseOffering()
    {
    }

    private CourseOffering(CourseOfferingId id, Guid courseId, Guid semesterId, Guid departmentId, int capacity, DateTimeOffset now)
    {
        Id = id;
        CourseId = courseId;
        SemesterId = semesterId;
        DepartmentId = departmentId;
        Capacity = capacity;
        EnrolledCount = 0;
        CreatedAt = now;
    }

    public Guid CourseId { get; private set; }

    public Guid SemesterId { get; private set; }

    public Guid DepartmentId { get; private set; }

    public int Capacity { get; private set; }

    /// <summary>See class remarks - read-only from this aggregate's own perspective; mutated exclusively via the repository's atomic conditional UPDATE.</summary>
    public int EnrolledCount { get; private set; }

    public Guid? InstructorFacultyMemberId { get; private set; }

    public DateTimeOffset? InstructorAssignedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyCollection<Section> Sections => _sections.AsReadOnly();

    public IReadOnlyCollection<Exam> Exams => _exams.AsReadOnly();

    public bool HasAvailableSeats => EnrolledCount < Capacity;

    public static CourseOffering Create(Guid courseId, Guid semesterId, Guid departmentId, int capacity, DateTimeOffset now)
    {
        if (capacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be at least 1.");
        }

        var offering = new CourseOffering(CourseOfferingId.New(), courseId, semesterId, departmentId, capacity, now);
        offering.Raise(new CourseOfferingPublished(offering.Id.Value, courseId, semesterId, now));
        return offering;
    }

    public Section AddSection(string code, Common.WeeklyTimeSlot schedule)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Section code is required.", nameof(code));
        }

        var section = new Section(SectionId.New(), Id, code.Trim(), schedule);
        _sections.Add(section);
        return section;
    }

    /// <summary>ACD-3, §7: the caller must have already validated instructor eligibility (Department match, `Active` status) via <c>IFacultyMemberLookup</c> BEFORE calling this - the aggregate itself only enforces "one active instructor at a time" and raises the event; it never reaches across modules itself.</summary>
    public void AssignInstructor(Guid facultyMemberId, DateTimeOffset now)
    {
        if (InstructorFacultyMemberId == facultyMemberId)
        {
            return;
        }

        InstructorFacultyMemberId = facultyMemberId;
        InstructorAssignedAt = now;
        Raise(new InstructorAssigned(facultyMemberId, Id.Value, now));
    }

    public void UnassignInstructor(DateTimeOffset now)
    {
        if (InstructorFacultyMemberId is not { } current)
        {
            return;
        }

        InstructorFacultyMemberId = null;
        InstructorAssignedAt = null;
        Raise(new InstructorUnassigned(current, Id.Value, now));
    }

    /// <summary>ACD-5: adds a gradable Exam. <paramref name="assessments"/>' weights, combined with every other Exam's Assessments already configured under this CourseOffering, must not exceed 1.0 (100%) in total.</summary>
    public Exam AddExam(string name, IReadOnlyCollection<(string Name, decimal Weight)> assessments)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Exam name is required.", nameof(name));
        }

        var existingWeight = _exams.SelectMany(e => e.Assessments).Sum(a => a.Weight);
        var newWeight = assessments.Sum(a => a.Weight);
        if (existingWeight + newWeight > 1.0m)
        {
            throw new InvalidOperationException($"Total Assessment weight for CourseOffering '{Id}' would exceed 100% ({existingWeight + newWeight:P0}).");
        }

        var exam = new Exam(ExamId.New(), Id, name.Trim());
        foreach (var (assessmentName, weight) in assessments)
        {
            if (weight <= 0 || weight > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(assessments), $"Assessment weight for '{assessmentName}' must be between 0 (exclusive) and 1 (inclusive).");
            }

            exam.AddAssessment(assessmentName, weight);
        }

        _exams.Add(exam);
        return exam;
    }
}

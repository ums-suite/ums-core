using UMS.Modules.Academic.Domain.Common;
using UMS.Shared.Domain;

namespace UMS.Modules.Academic.Domain.AcademicSessions;

/// <summary>
/// ACD-3 (bundled foundation): a year-scoped academic period containing Semesters
/// (requirement-spec.md §2, docs/ddd/ubiquitous-language.md). Not itself a separately numbered
/// ticket - bundled into ACD-3's own scope because `CourseOffering` (ACD-3's actual ticket) cannot
/// schedule a Course "in a Semester" that doesn't yet exist, the same class of judgment call this
/// build documents for `Program` under ACD-1.
/// </summary>
public sealed class AcademicSession : AggregateRoot<AcademicSessionId>
{
    private readonly List<Semester> _semesters = [];

    private AcademicSession()
    {
    }

    private AcademicSession(AcademicSessionId id, AcademicSessionCode code, DateTimeOffset now)
    {
        Id = id;
        Code = code;
        CreatedAt = now;
    }

    public AcademicSessionCode Code { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyCollection<Semester> Semesters => _semesters.AsReadOnly();

    public static AcademicSession Create(AcademicSessionCode code, DateTimeOffset now) => new(AcademicSessionId.New(), code, now);

    public Semester AddSemester(string name, DateRange registrationWindow, DateRange dropWindow)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Semester name is required.", nameof(name));
        }

        var semester = new Semester(SemesterId.New(), Id, name.Trim(), registrationWindow, dropWindow);
        _semesters.Add(semester);
        return semester;
    }
}

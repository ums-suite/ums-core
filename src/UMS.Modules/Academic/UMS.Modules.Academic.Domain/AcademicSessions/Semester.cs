using UMS.Shared.Domain;

namespace UMS.Modules.Academic.Domain.AcademicSessions;

/// <summary>
/// A single teaching term within an AcademicSession, defining a registration window
/// (requirement-spec.md §2 Academic Session, Semester &amp; Course Offering Setup) and an
/// administratively configured drop window (§2 Semester Registration: "course withdrawal is legal
/// only within an administratively configured drop window").
/// </summary>
public sealed class Semester
{
    internal Semester(SemesterId id, AcademicSessionId academicSessionId, string name, DateRange registrationWindow, DateRange dropWindow)
    {
        Id = id;
        AcademicSessionId = academicSessionId;
        Name = name;
        RegistrationWindow = registrationWindow;
        DropWindow = dropWindow;
    }

    private Semester()
    {
    }

    public SemesterId Id { get; private set; }

    public AcademicSessionId AcademicSessionId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public DateRange RegistrationWindow { get; private set; } = null!;

    public DateRange DropWindow { get; private set; } = null!;

    public bool IsRegistrationOpen(DateTimeOffset now) => RegistrationWindow.Contains(now);

    public bool IsDropWindowOpen(DateTimeOffset now) => DropWindow.Contains(now);
}

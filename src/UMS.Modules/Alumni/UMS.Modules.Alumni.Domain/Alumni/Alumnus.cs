using UMS.Modules.Alumni.Domain.Common;
using UMS.Modules.Alumni.Domain.Events;

namespace UMS.Modules.Alumni.Domain.Alumni;

/// <summary>
/// ALM-1/ALM-2/ALM-3: the Alumnus aggregate root (requirement-spec.md §2.1/§2.2, §3, §4).
///
/// <para>
/// Created exactly once per <see cref="StudentIdRef"/> (design-decisions.md "Idempotent
/// StudentGraduated Consumption" - a database UNIQUE constraint on <c>student_id_ref</c> is the ONLY
/// mechanism; <see cref="Domain.Alumni"/>'s own creation call site never re-checks existence in
/// application code first, since that would reopen the exact TOCTOU race the constraint exists to
/// close). <see cref="GraduationYear"/>/<see cref="ProgramId"/>/<see cref="DepartmentId"/> are seeded
/// once at creation and never re-derived from Student's own schema afterwards (§4: "Alumnus profile
/// data is independent of Student's live record after creation").
/// </para>
///
/// <para>
/// <b>Deliberate interpretation of an underspecified requirement</b> (documented in the PR, not a
/// silent deviation): §2.1 says these three fields are "pulled from the event payload" - but
/// Student's own already-merged <c>StudentGraduated</c> domain event carries only
/// <c>(StudentId, OccurredAt)</c>, no graduation year/program/department. Modifying Student's already-
/// shipped event is out of scope and risky (a separate, already-shipped module). Instead: Program/
/// Department are resolved via the already-built <c>UMS.Shared.Student.IStudentStatusChecker</c>
/// shared contract - the sanctioned indirect cross-module read path every other module already uses,
/// not a forbidden direct query into Student's schema (ADR-0002) - and the graduation year is derived
/// from the event's own <c>OccurredAt</c> timestamp. This satisfies the spirit of "never re-queried
/// from Student's schema directly" exactly as intended.
/// </para>
/// </summary>
public sealed class Alumnus : AggregateRoot<AlumnusId>
{
    private Alumnus()
    {
    }

    private Alumnus(AlumnusId id, Guid studentIdRef, int graduationYear, Guid programId, Guid departmentId, DateTimeOffset now)
    {
        Id = id;
        StudentIdRef = studentIdRef;
        GraduationYear = graduationYear;
        ProgramId = programId;
        DepartmentId = departmentId;
        ProfileVisibility = ProfileVisibility.Private;
        CreatedAt = now;
    }

    public Guid StudentIdRef { get; private set; }

    public int GraduationYear { get; private set; }

    public Guid ProgramId { get; private set; }

    public Guid DepartmentId { get; private set; }

    public ProfileVisibility ProfileVisibility { get; private set; }

    public string? CurrentEmployer { get; private set; }

    public string? Bio { get; private set; }

    public string? Location { get; private set; }

    public string? ContactEmail { get; private set; }

    public string? ContactPhone { get; private set; }

    /// <summary>requirement-spec.md §2.2 field-level visibility: hides <see cref="CurrentEmployer"/> from directory reads even while <see cref="ProfileVisibility"/> is <see cref="Domain.Alumni.ProfileVisibility.Public"/>.</summary>
    public bool HideCurrentEmployer { get; private set; }

    /// <summary>Hides <see cref="ContactEmail"/>/<see cref="ContactPhone"/> from directory reads independent of the record-level flag.</summary>
    public bool HideContactDetails { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static Alumnus Create(Guid studentIdRef, int graduationYear, Guid programId, Guid departmentId, DateTimeOffset now)
    {
        var alumnus = new Alumnus(AlumnusId.New(), studentIdRef, graduationYear, programId, departmentId, now);
        alumnus.Raise(new AlumnusCreated(alumnus.Id.Value, studentIdRef, now));
        return alumnus;
    }

    /// <summary>ALM-2: self-registered employer/bio/contact/photo-adjacent fields plus field-level visibility toggles (requirement-spec.md §2.1 last bullet, §2.2).</summary>
    public void UpdateProfile(string? currentEmployer, string? bio, string? location, string? contactEmail, string? contactPhone, bool hideCurrentEmployer, bool hideContactDetails)
    {
        CurrentEmployer = string.IsNullOrWhiteSpace(currentEmployer) ? null : currentEmployer.Trim();
        Bio = string.IsNullOrWhiteSpace(bio) ? null : bio.Trim();
        Location = string.IsNullOrWhiteSpace(location) ? null : location.Trim();
        ContactEmail = string.IsNullOrWhiteSpace(contactEmail) ? null : contactEmail.Trim();
        ContactPhone = string.IsNullOrWhiteSpace(contactPhone) ? null : contactPhone.Trim();
        HideCurrentEmployer = hideCurrentEmployer;
        HideContactDetails = hideContactDetails;
    }

    /// <summary>
    /// requirement-spec.md §4/§9: opt-in record-level visibility, self-service by default. The same
    /// method backs an Admin override (§5 Auditability) - the CALLING application service is
    /// responsible for recording the audited access when the caller is Admin acting on someone else's
    /// record, not this domain method (mirrors every other module's own "domain enforces the
    /// invariant, application layer enforces who may call it and audits it" split).
    /// </summary>
    public void SetVisibility(ProfileVisibility visibility) => ProfileVisibility = visibility;
}

using UMS.Modules.Student.Domain.Common;
using UMS.Modules.Student.Domain.Events;
using UMS.Modules.Student.Domain.Guardians;
using UMS.Shared.Domain;

namespace UMS.Modules.Student.Domain.Students;

/// <summary>
/// STU-1/STU-5..STU-8: the enrolled-identity aggregate root (requirement-spec.md student §2/§3/§4).
/// Not to be confused with the Applicant it was created from (Admission-owned, glossary) - once
/// <see cref="Enroll"/> runs, this is the sole authoritative record; a later <c>Graduated</c>
/// transition does not delete or migrate it (edge-cases.md, "Transcript request for a Graduated
/// student").
///
/// <para>
/// Identity-bearing fields (<see cref="Name"/>, <see cref="DateOfBirth"/>, <see cref="NationalId"/>)
/// are set once at <see cref="Enroll"/> time and have no public mutator - requirement-spec.md §9
/// decision 2: a change would need a <c>StudentRequest</c>-style approval workflow, deliberately
/// out of this Core pass's scope (release/DEVELOPMENT_PLAN.md row 11/16). <see cref="ContactEmail"/>/
/// <see cref="ContactPhone"/>/<see cref="PhotoUrl"/> are the self-service-editable subset
/// (<see cref="UpdateSelfServiceProfile"/>), deliberately separate from the enrollment-time
/// <see cref="Email"/>/<see cref="Mobile"/> used only to provision the backing Identity
/// <c>User</c> - mirroring Faculty's own "self-service subset is a strict field-scoped slice of the
/// full profile" pattern (edge-cases.md faculty, "Self-Service Update Racing an HR/Registrar Full
/// Edit").
/// </para>
/// </summary>
public sealed class Student : AggregateRoot<StudentId>
{
    private static readonly Dictionary<StudentStatus, StudentStatus[]> _legalTransitions = new()
    {
        [StudentStatus.Enrolled] = [StudentStatus.Active],
        [StudentStatus.Active] = [StudentStatus.Graduated, StudentStatus.Suspended, StudentStatus.Transferred],
        [StudentStatus.Suspended] = [StudentStatus.Active],
        [StudentStatus.Graduated] = [],
        [StudentStatus.Transferred] = [],
    };

    private readonly List<StudentStatusHistoryEntry> _statusHistory = [];
    private readonly List<Guardian> _guardians = [];
    private readonly List<GuardianAccessGrant> _guardianAccessGrants = [];

    private Student()
    {
    }

    private Student(
        StudentId id,
        Guid originatingApplicationId,
        StudentNumber studentNumber,
        Guid departmentId,
        Guid programId,
        PersonName name,
        Email email,
        PhoneNumber? mobile,
        DateOnly dateOfBirth,
        string? nationalId,
        DateTimeOffset now)
    {
        Id = id;
        OriginatingApplicationId = originatingApplicationId;
        StudentNumber = studentNumber;
        DepartmentId = departmentId;
        ProgramId = programId;
        Name = name;
        Email = email;
        Mobile = mobile;
        DateOfBirth = dateOfBirth;
        NationalId = string.IsNullOrWhiteSpace(nationalId) ? null : nationalId.Trim();
        Status = StudentStatus.Enrolled;
        CreatedAt = now;
    }

    public Guid OriginatingApplicationId { get; private set; }

    public StudentNumber StudentNumber { get; private set; } = null!;

    public Guid DepartmentId { get; private set; }

    public Guid ProgramId { get; private set; }

    public PersonName Name { get; private set; } = null!;

    public Email Email { get; private set; } = null!;

    public PhoneNumber? Mobile { get; private set; }

    public DateOnly DateOfBirth { get; private set; }

    public string? NationalId { get; private set; }

    public StudentStatus Status { get; private set; }

    /// <summary>Best-effort side effect of <see cref="Enroll"/> (STU-2) - <c>null</c> if Identity provisioning failed; Student creation itself is never rolled back by that failure (see <c>CreateStudentRecordService</c>'s own remarks).</summary>
    public Guid? IdentityUserId { get; private set; }

    /// <summary>Best-effort side effect of <see cref="Enroll"/> (STU-3) - <c>null</c> if the Documents ID-card request failed/degraded.</summary>
    public Guid? IdCardDocumentId { get; private set; }

    public string? ContactEmail { get; private set; }

    public string? ContactPhone { get; private set; }

    public string? PhotoUrl { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyCollection<StudentStatusHistoryEntry> StatusHistory => _statusHistory.AsReadOnly();

    public IReadOnlyCollection<Guardian> Guardians => _guardians.AsReadOnly();

    public IReadOnlyCollection<GuardianAccessGrant> GuardianAccessGrants => _guardianAccessGrants.AsReadOnly();

    /// <summary>STU-1: raises <see cref="StudentRecordCreated"/> - the caller (<c>CreateStudentRecordService</c>) must only reach this factory from its own first-successful-insert branch, never its idempotent lookup-and-return branch (design-decisions.md).</summary>
    public static Student Enroll(
        Guid originatingApplicationId,
        StudentNumber studentNumber,
        Guid departmentId,
        Guid programId,
        PersonName name,
        Email email,
        PhoneNumber? mobile,
        DateOnly dateOfBirth,
        string? nationalId,
        DateTimeOffset now)
    {
        var student = new Student(StudentId.New(), originatingApplicationId, studentNumber, departmentId, programId, name, email, mobile, dateOfBirth, nationalId, now);
        student._statusHistory.Add(StudentStatusHistoryEntry.Create(fromStatus: null, StudentStatus.Enrolled, "Student record created.", changedByUserId: null, now));
        student.Raise(new StudentRecordCreated(student.Id.Value, originatingApplicationId, studentNumber.Value, now));
        return student;
    }

    /// <summary>STU-2: best-effort - see <see cref="IdentityUserId"/>'s own remarks.</summary>
    public void SetIdentityUser(Guid userId) => IdentityUserId = userId;

    /// <summary>STU-3: best-effort - see <see cref="IdCardDocumentId"/>'s own remarks.</summary>
    public void SetIdCardDocument(Guid documentId) => IdCardDocumentId = documentId;

    /// <summary>STU-6: field-scoped self-service write - contact info and photo only (requirement-spec.md §2/§9 decision 2; §6 <c>PUT /students/me</c>).</summary>
    public void UpdateSelfServiceProfile(string? contactEmail, string? contactPhone, string? photoUrl)
    {
        ContactEmail = string.IsNullOrWhiteSpace(contactEmail) ? null : contactEmail.Trim();
        ContactPhone = string.IsNullOrWhiteSpace(contactPhone) ? null : contactPhone.Trim();
        PhotoUrl = string.IsNullOrWhiteSpace(photoUrl) ? null : photoUrl.Trim();
    }

    /// <summary>
    /// STU-8: the full status state machine (requirement-spec.md §2/§4). Throws
    /// <see cref="InvalidOperationException"/> for any transition <see cref="_legalTransitions"/>
    /// does not name - "no skipped or backward transition outside the two explicitly modeled
    /// exceptions" (§4). Appends a <see cref="StudentStatusHistoryEntry"/> and raises
    /// <see cref="StudentStatusChanged"/> plus the specific <see cref="StudentSuspended"/>/
    /// <see cref="StudentGraduated"/>/<see cref="StudentTransferred"/> event, in the SAME call the
    /// caller wraps in one database transaction alongside the aggregate's own save
    /// (design-decisions.md, "Append-Only StudentStatusHistory Write Pattern").
    /// </summary>
    public void ChangeStatus(StudentStatus newStatus, string? reason, Guid changedByUserId, DateTimeOffset now)
    {
        if (!_legalTransitions.TryGetValue(Status, out var allowed) || !allowed.Contains(newStatus))
        {
            throw new InvalidOperationException($"Cannot transition a Student from {Status} to {newStatus} - this is not a legal status transition.");
        }

        var previous = Status;
        Status = newStatus;
        _statusHistory.Add(StudentStatusHistoryEntry.Create(previous, newStatus, reason, changedByUserId, now));
        Raise(new StudentStatusChanged(Id.Value, previous, newStatus, now));

        switch (newStatus)
        {
            case StudentStatus.Suspended:
                Raise(new StudentSuspended(Id.Value, reason, now));
                break;
            case StudentStatus.Graduated:
                Raise(new StudentGraduated(Id.Value, now));
                break;
            case StudentStatus.Transferred:
                Raise(new StudentTransferred(Id.Value, reason, now));
                break;
        }
    }

    /// <summary>Guardian/GuardianAccessGrant scaffolding (docs/ddd/ubiquitous-language.md) - Student-owned, self-service.</summary>
    public Guardian LinkGuardian(string name, string relationship, string? contactEmail, string? contactPhone, DateTimeOffset now)
    {
        var guardian = Guardian.Link(Id, name, relationship, contactEmail, contactPhone, now);
        _guardians.Add(guardian);
        Raise(new GuardianLinked(Id.Value, guardian.Id.Value, now));
        return guardian;
    }

    /// <summary>"No default/implicit visibility ever, every category is a separate explicit grant" (this module's own PR-documented design decision).</summary>
    public GuardianAccessGrant GrantGuardianAccess(Guid guardianId, GuardianAccessCategory category, DateTimeOffset now)
    {
        if (_guardians.All(g => g.Id.Value != guardianId))
        {
            throw new InvalidOperationException($"No Guardian '{guardianId}' is linked to this Student.");
        }

        if (_guardianAccessGrants.Any(g => g.GuardianId == guardianId && g.Category == category && g.IsActive))
        {
            throw new InvalidOperationException($"Guardian '{guardianId}' already holds an active grant for category '{category}'.");
        }

        var grant = GuardianAccessGrant.Create(guardianId, Id, category, now);
        _guardianAccessGrants.Add(grant);
        Raise(new GuardianAccessGranted(Id.Value, guardianId, category, now));
        return grant;
    }

    /// <summary>Student-revocable at any time (docs/ddd/ubiquitous-language.md, <c>GuardianAccessGrant</c>).</summary>
    public void RevokeGuardianAccess(Guid guardianId, GuardianAccessCategory category, DateTimeOffset now)
    {
        var grant = _guardianAccessGrants.FirstOrDefault(g => g.GuardianId == guardianId && g.Category == category && g.IsActive)
            ?? throw new InvalidOperationException($"Guardian '{guardianId}' holds no active grant for category '{category}' to revoke.");

        grant.Revoke(now);
        Raise(new GuardianAccessRevoked(Id.Value, guardianId, category, now));
    }
}

using UMS.Modules.Faculty.Domain.Common;
using UMS.Modules.Faculty.Domain.Events;

namespace UMS.Modules.Faculty.Domain.FacultyMembers;

/// <summary>
/// FAC-1: the employment profile aggregate root (requirement-spec.md faculty §2 Employment
/// Profile, §3). Not to be confused with <c>UMS.Modules.Organization.Domain.Faculties.Faculty</c>,
/// the academic org-unit ("Faculty of Engineering") - this type models a *person* (glossary:
/// "FacultyMember").
///
/// <para>
/// <see cref="IsDepartmentHead"/> is a first-pass design decision, not a BRD-specified field:
/// requirement-spec.md §4/§8 requires that a Department Head cannot approve their own
/// LeaveRequest, but neither Organization nor any other module tracks "who heads this
/// Department" today - Organization's own Department aggregate has no headship concept
/// (module-boundaries.md). Faculty tracks it directly, HR-settable via the full-edit employment
/// scope, since LeaveRequest's self-approval-routing invariant needs some source of truth for it.
/// </para>
/// </summary>
public sealed class FacultyMember : AggregateRoot<FacultyMemberId>
{
    private FacultyMember()
    {
    }

    private FacultyMember(FacultyMemberId id, Guid userId, string employeeId, Guid departmentId, Guid designationId, EmploymentType employmentType, DateOnly joiningDate, DateTimeOffset now)
    {
        Id = id;
        UserId = userId;
        EmployeeId = employeeId;
        DepartmentId = departmentId;
        DesignationId = designationId;
        EmploymentType = employmentType;
        JoiningDate = joiningDate;
        Status = FacultyMemberStatus.Active;
        CreatedAt = now;
    }

    /// <summary>The Identity <c>UserId</c> this profile belongs to - determines "owning FacultyMember" for self-service writes (requirement-spec.md §2).</summary>
    public Guid UserId { get; private set; }

    public string EmployeeId { get; private set; } = string.Empty;

    public Guid DepartmentId { get; private set; }

    public Guid DesignationId { get; private set; }

    public EmploymentType EmploymentType { get; private set; }

    public FacultyMemberStatus Status { get; private set; }

    public bool IsDepartmentHead { get; private set; }

    public string? ContactEmail { get; private set; }

    public string? ContactPhone { get; private set; }

    public DateOnly JoiningDate { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static FacultyMember Onboard(Guid userId, string employeeId, Guid departmentId, Guid designationId, EmploymentType employmentType, DateOnly joiningDate, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(employeeId))
        {
            throw new ArgumentException("Employee id is required.", nameof(employeeId));
        }

        var facultyMember = new FacultyMember(FacultyMemberId.New(), userId, employeeId.Trim(), departmentId, designationId, employmentType, joiningDate, now);
        facultyMember.Raise(new FacultyMemberOnboarded(facultyMember.Id.Value, userId, departmentId, facultyMember.EmployeeId, now));
        return facultyMember;
    }

    /// <summary>
    /// edge-cases.md "FacultyMember Self-Service Update Racing an HR/Registrar Full Edit":
    /// field-scoped write - only the bounded self-service subset (contact info) is touched here.
    /// The caller (Application service) is responsible for rejecting an out-of-scope field before
    /// this is ever called (design-decisions.md, "FacultyMember Concurrent-Write Resolution").
    /// </summary>
    public void UpdateSelfServiceProfile(string? contactEmail, string? contactPhone)
    {
        ContactEmail = string.IsNullOrWhiteSpace(contactEmail) ? null : contactEmail.Trim();
        ContactPhone = string.IsNullOrWhiteSpace(contactPhone) ? null : contactPhone.Trim();
    }

    /// <summary>HR/Registrar full edit - may touch any field, including the self-service subset (requirement-spec.md §2, design-decisions.md).</summary>
    public void UpdateEmploymentDetails(Guid departmentId, Guid designationId, EmploymentType employmentType, bool isDepartmentHead, string? contactEmail, string? contactPhone)
    {
        DepartmentId = departmentId;
        DesignationId = designationId;
        EmploymentType = employmentType;
        IsDepartmentHead = isDepartmentHead;
        ContactEmail = string.IsNullOrWhiteSpace(contactEmail) ? null : contactEmail.Trim();
        ContactPhone = string.IsNullOrWhiteSpace(contactPhone) ? null : contactPhone.Trim();
    }

    public void ChangeStatus(FacultyMemberStatus newStatus, DateTimeOffset now)
    {
        if (Status == newStatus)
        {
            throw new InvalidOperationException($"FacultyMember is already {newStatus}.");
        }

        var previous = Status;
        Status = newStatus;
        Raise(new FacultyMemberStatusChanged(Id.Value, previous, newStatus, now));
    }
}

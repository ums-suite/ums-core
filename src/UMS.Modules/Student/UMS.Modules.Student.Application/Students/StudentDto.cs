namespace UMS.Modules.Student.Application.Students;

public sealed record StudentDto(
    Guid Id,
    string StudentNumber,
    Guid DepartmentId,
    Guid ProgramId,
    string GivenName,
    string FamilyName,
    string? GivenNameBn,
    string? FamilyNameBn,
    string Email,
    string? Mobile,
    DateOnly DateOfBirth,
    string? NationalId,
    string Status,
    Guid? IdentityUserId,
    Guid? IdCardDocumentId,
    string? ContactEmail,
    string? ContactPhone,
    string? PhotoUrl,
    DateTimeOffset CreatedAt,
    uint Version);

/// <summary>STU-1's internal-only command shape (§6) - see <c>UMS.Shared.Student.CreateStudentRecordCommand</c>'s own remarks for the field-by-field rationale this mirrors exactly.</summary>
public sealed record CreateStudentRecordRequest(
    Guid OriginatingApplicationId,
    int AdmissionYear,
    string FacultyCode,
    Guid DepartmentId,
    Guid ProgramId,
    string GivenName,
    string FamilyName,
    string? GivenNameBn,
    string? FamilyNameBn,
    string Email,
    string? Mobile,
    DateOnly DateOfBirth,
    string? NationalId);

/// <summary>STU-6: field-scoped self-service write - contact info and photo only (requirement-spec.md §2/§9 decision 2).</summary>
public sealed record UpdateSelfServiceProfileRequest(string? ContactEmail, string? ContactPhone, string? PhotoUrl, uint Version);

public sealed record ChangeStudentStatusRequest(string Status, string? Reason, uint Version);

/// <summary>Returned on a version conflict (design-decisions.md, "reject and show the current state, don't blind-retry") - the loser is shown exactly what actually happened instead of a bare error.</summary>
public sealed record StudentStatusConflict(StudentDto CurrentState);

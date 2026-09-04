namespace UMS.Modules.Faculty.Application.FacultyMembers;

public sealed record FacultyMemberDto(
    Guid Id,
    Guid UserId,
    string EmployeeId,
    Guid DepartmentId,
    Guid DesignationId,
    string EmploymentType,
    string Status,
    bool IsDepartmentHead,
    string? ContactEmail,
    string? ContactPhone,
    DateOnly JoiningDate,
    DateTimeOffset CreatedAt,
    uint Version);

public sealed record OnboardFacultyMemberRequest(Guid UserId, string EmployeeId, Guid DepartmentId, Guid DesignationId, string EmploymentType, DateOnly JoiningDate);

/// <summary>Field-scoped self-service write - contact info only (edge-cases.md, "FacultyMember Self-Service Update Racing an HR/Registrar Full Edit").</summary>
public sealed record UpdateSelfServiceProfileRequest(string? ContactEmail, string? ContactPhone, uint Version);

public sealed record UpdateEmploymentDetailsRequest(Guid DepartmentId, Guid DesignationId, string EmploymentType, bool IsDepartmentHead, string? ContactEmail, string? ContactPhone, uint Version);

public sealed record ChangeFacultyMemberStatusRequest(string Status, uint Version);

public sealed record FacultyMemberListPage(IReadOnlyList<FacultyMemberDto> Items, int TotalCount, int Skip, int Take);

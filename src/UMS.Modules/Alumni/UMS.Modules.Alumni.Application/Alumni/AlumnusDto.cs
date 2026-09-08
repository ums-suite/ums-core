namespace UMS.Modules.Alumni.Application.Alumni;

public sealed record AlumnusDto(
    Guid Id,
    Guid StudentIdRef,
    int GraduationYear,
    Guid ProgramId,
    Guid DepartmentId,
    string ProfileVisibility,
    string? CurrentEmployer,
    string? Bio,
    string? Location,
    string? ContactEmail,
    string? ContactPhone,
    bool HideCurrentEmployer,
    bool HideContactDetails,
    DateTimeOffset CreatedAt,
    uint Version);

/// <summary>ALM-3: a directory search hit - field-level-hidden values are nulled out here rather than in <see cref="AlumnusDto"/>, which stays the full self/Admin projection.</summary>
public sealed record AlumniDirectoryEntryDto(
    Guid Id,
    int GraduationYear,
    Guid ProgramId,
    Guid DepartmentId,
    string? CurrentEmployer,
    string? Location,
    string? ContactEmail,
    string? ContactPhone);

public sealed record AlumniDirectoryPage(IReadOnlyList<AlumniDirectoryEntryDto> Items, int Skip, int Take);

public sealed record UpdateAlumnusProfileRequest(string? CurrentEmployer, string? Bio, string? Location, string? ContactEmail, string? ContactPhone, bool HideCurrentEmployer, bool HideContactDetails);

public sealed record SetProfileVisibilityRequest(string Visibility);

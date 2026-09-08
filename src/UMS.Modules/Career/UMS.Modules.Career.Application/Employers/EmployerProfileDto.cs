namespace UMS.Modules.Career.Application.Employers;

public sealed record EmployerProfileDto(
    Guid Id,
    string CompanyName,
    string Industry,
    string? Website,
    string ContactName,
    string ContactEmail,
    string? ContactPhone,
    string? VerificationNote,
    bool IsArchived,
    DateTimeOffset CreatedAt,
    uint Version);

public sealed record CreateEmployerProfileRequest(string CompanyName, string Industry, string? Website, string ContactName, string ContactEmail, string? ContactPhone, string? VerificationNote);

public sealed record UpdateEmployerProfileRequest(string CompanyName, string Industry, string? Website, string ContactName, string ContactEmail, string? ContactPhone, string? VerificationNote, uint Version);

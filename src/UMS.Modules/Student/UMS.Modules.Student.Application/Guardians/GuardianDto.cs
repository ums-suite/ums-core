namespace UMS.Modules.Student.Application.Guardians;

public sealed record GuardianDto(
    Guid Id,
    Guid StudentId,
    string Name,
    string Relationship,
    string? ContactEmail,
    string? ContactPhone,
    DateTimeOffset LinkedAt,
    IReadOnlyList<GuardianAccessGrantDto> ActiveAccessGrants);

public sealed record GuardianAccessGrantDto(Guid Id, string Category, DateTimeOffset GrantedAt);

/// <summary>STU Guardian scaffolding (docs/ddd/ubiquitous-language.md) - "at least one of contact email or contact phone" is enforced by <c>Guardian.Link</c> itself (ums-conventions.md, Domain Modeling).</summary>
public sealed record LinkGuardianRequest(string Name, string Relationship, string? ContactEmail, string? ContactPhone);

public sealed record GrantGuardianAccessRequest(string Category);

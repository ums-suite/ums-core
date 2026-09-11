namespace UMS.Modules.Career.Application.Internships;

public sealed record InternshipDto(
    Guid Id,
    Guid EmployerProfileId,
    string Title,
    string Description,
    string Location,
    string? StipendNote,
    DateTimeOffset ApplicationDeadline,
    string Status,
    string? WithdrawalReason,
    EligibilityCriteriaDto Eligibility,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PublishedAt,
    uint Version);

public sealed record EligibilityCriteriaDto(IReadOnlyCollection<Guid> ProgramIds, decimal? MinCgpa, int? MinYearOfStudy);

public sealed record CreateInternshipRequest(Guid EmployerProfileId, string Title, string Description, string Location, string? StipendNote, DateTimeOffset ApplicationDeadline, EligibilityCriteriaDto Eligibility);

public sealed record EditInternshipRequest(string Title, string Description, string Location, string? StipendNote, DateTimeOffset ApplicationDeadline, EligibilityCriteriaDto Eligibility, uint Version);

public sealed record WithdrawInternshipRequest(string Reason, uint Version);

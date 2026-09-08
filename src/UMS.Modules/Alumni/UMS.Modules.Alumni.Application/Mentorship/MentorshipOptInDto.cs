namespace UMS.Modules.Alumni.Application.Mentorship;

public sealed record MentorshipOptInDto(Guid Id, Guid PersonId, string Role, string ExpertiseAreas, int CapacityLimit, int ActiveCount, string? Availability, bool IsActive, DateTimeOffset CreatedAt);

public sealed record OptInRequest(string Role, string ExpertiseAreas, int CapacityLimit, string? Availability);

public sealed record MentorshipMatchDto(
    Guid Id,
    Guid MentorAlumnusId,
    Guid MenteeStudentId,
    string Status,
    DateTimeOffset ProposedAt,
    DateTimeOffset? MentorAcceptedAt,
    DateTimeOffset? MenteeAcceptedAt,
    DateTimeOffset? ActivatedAt,
    DateTimeOffset? EndedAt,
    string? EndedReason);

public sealed record ProposeMatchRequest(Guid MentorAlumnusId, Guid MenteeStudentId);

public sealed record EndMatchRequest(string Reason);

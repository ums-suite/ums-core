namespace UMS.Modules.Alumni.Application.Jobs;

public sealed record JobPostingDto(
    Guid Id,
    Guid PosterUserId,
    bool PosterIsAlumnus,
    Guid? PosterAlumnusId,
    string Title,
    string Company,
    string Description,
    string Location,
    string ContactMethod,
    DateTimeOffset ExpiresAt,
    string Status,
    string? ModerationReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PublishedAt,
    uint Version);

public sealed record PostJobRequest(string Title, string Company, string Description, string Location, string ContactMethod, DateTimeOffset ExpiresAt);

public sealed record EditJobPostingRequest(string Title, string Company, string Description, string Location, string ContactMethod, DateTimeOffset ExpiresAt, uint Version);

public sealed record ModerateJobPostingRequest(bool Approve, string? Reason, uint Version);

public sealed record VersionedActionRequest(uint Version);

public sealed record RemoveJobPostingRequest(string Reason, uint Version);

namespace UMS.Modules.Faculty.Application.ResearchProfiles;

public sealed record ResearchProfileDto(Guid Id, Guid FacultyMemberId, IReadOnlyList<PublicationDto> Publications, string? OngoingResearch, string? Grants, uint Version);

public sealed record PublicationDto(string Title, string Venue, int Year, string? Url);

public sealed record UpdateResearchProfileRequest(IReadOnlyList<PublicationDto> Publications, string? OngoingResearch, string? Grants, uint Version);

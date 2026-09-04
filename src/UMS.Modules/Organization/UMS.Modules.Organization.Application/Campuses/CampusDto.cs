namespace UMS.Modules.Organization.Application.Campuses;

public sealed record CampusDto(Guid Id, Guid UniversityId, string Name, string Status, DateTimeOffset CreatedAt, uint Version);

public sealed record CreateCampusRequest(Guid UniversityId, string Name);

public sealed record UpdateCampusRequest(string? Name, string? Status, uint Version);

public sealed record CampusListPage(IReadOnlyList<CampusDto> Items, int TotalCount, int Skip, int Take);

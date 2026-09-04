namespace UMS.Modules.Organization.Application.Facilities;

public sealed record BuildingDto(Guid Id, Guid CampusId, string Name, string? Code, DateTimeOffset CreatedAt);

public sealed record CreateBuildingRequest(Guid CampusId, string Name, string? Code);

public sealed record BuildingListPage(IReadOnlyList<BuildingDto> Items, int TotalCount, int Skip, int Take);

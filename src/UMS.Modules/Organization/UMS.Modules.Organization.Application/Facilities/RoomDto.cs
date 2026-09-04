namespace UMS.Modules.Organization.Application.Facilities;

public sealed record RoomDto(Guid Id, Guid BuildingId, string Name, int? Capacity, string? RoomType, DateTimeOffset CreatedAt);

public sealed record CreateRoomRequest(Guid BuildingId, string Name, int? Capacity, string? RoomType);

public sealed record RoomListPage(IReadOnlyList<RoomDto> Items, int TotalCount, int Skip, int Take);

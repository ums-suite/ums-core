namespace UMS.Modules.Hostel.Application.Hostels;

public sealed record HostelDto(Guid Id, string Name, string Type, DateTimeOffset CreatedAt);

public sealed record BuildingDto(Guid Id, Guid HostelId, string Name, DateTimeOffset CreatedAt);

public sealed record RoomDto(Guid Id, Guid BuildingId, Guid HostelId, string RoomNumber, string Type, int Capacity, DateTimeOffset CreatedAt);

public sealed record BedDto(Guid Id, Guid RoomId, string Label, DateTimeOffset CreatedAt);

public sealed record CreateHostelRequest(string Name, string Type);

public sealed record CreateBuildingRequest(Guid HostelId, string Name);

public sealed record CreateRoomRequest(Guid BuildingId, string RoomNumber, string Type, int Capacity);

public sealed record ChangeRoomCapacityRequest(int NewCapacity);

public sealed record CreateBedRequest(Guid RoomId, string Label);

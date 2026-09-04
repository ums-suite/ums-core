using UMS.Modules.Organization.Application.Common;
using UMS.Modules.Organization.Application.Facilities;
using UMS.Modules.Organization.Domain.Facilities;
using UMS.Modules.Organization.UnitTests.TestDoubles;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Organization.UnitTests.Facilities;

/// <summary>edge-cases.md "Hard-delete cross-module reference check races a new reference being created" - exercised here via the stub <see cref="FakeRoomReferenceChecker"/>.</summary>
public class RoomServiceTests
{
    private static readonly AuditContext _audit = new(Guid.NewGuid(), "127.0.0.1", "test-correlation");

    [Fact]
    public async Task DeleteAsync_rejects_a_Room_another_module_still_references()
    {
        var room = Room.Create(BuildingId.New(), "101", null, null, DateTimeOffset.UtcNow);
        var rooms = new FakeRoomRepository();
        rooms.Seed(room);

        var service = new RoomService(rooms, new FakeBuildingRepository(), new FakeRoomReferenceChecker(hasReferences: true), new FakeUnitOfWork(), new FakeAuditRecorder(), new FakeClock());

        var result = await service.DeleteAsync(room.Id.Value, _audit);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error!.Type);
        Assert.Equal("room.has_references", result.Error.Code);
    }

    [Fact]
    public async Task DeleteAsync_succeeds_for_an_unreferenced_Room()
    {
        var room = Room.Create(BuildingId.New(), "101", null, null, DateTimeOffset.UtcNow);
        var rooms = new FakeRoomRepository();
        rooms.Seed(room);
        var auditRecorder = new FakeAuditRecorder();

        var service = new RoomService(rooms, new FakeBuildingRepository(), new FakeRoomReferenceChecker(hasReferences: false), new FakeUnitOfWork(), auditRecorder, new FakeClock());

        var result = await service.DeleteAsync(room.Id.Value, _audit);

        Assert.True(result.IsSuccess);
        Assert.Contains(auditRecorder.RecordedEntries, e => e.Action == "delete" && e.EntityType == "Room");
    }

    [Fact]
    public async Task DeleteAsync_returns_NotFound_for_an_unknown_Room()
    {
        var service = new RoomService(new FakeRoomRepository(), new FakeBuildingRepository(), new FakeRoomReferenceChecker(), new FakeUnitOfWork(), new FakeAuditRecorder(), new FakeClock());

        var result = await service.DeleteAsync(Guid.NewGuid(), _audit);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
    }
}

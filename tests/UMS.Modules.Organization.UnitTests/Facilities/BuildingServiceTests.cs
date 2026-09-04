using UMS.Modules.Organization.Application.Common;
using UMS.Modules.Organization.Application.Facilities;
using UMS.Modules.Organization.Domain.Campuses;
using UMS.Modules.Organization.Domain.Facilities;
using UMS.Modules.Organization.UnitTests.TestDoubles;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Organization.UnitTests.Facilities;

/// <summary>design-decisions.md, "Soft-Delete/Deactivate-Only Pattern": hard delete is permitted for Building, gated on zero Rooms beneath it.</summary>
public class BuildingServiceTests
{
    private static readonly AuditContext _audit = new(Guid.NewGuid(), "127.0.0.1", "test-correlation");

    [Fact]
    public async Task DeleteAsync_rejects_a_Building_that_still_has_Rooms()
    {
        var building = Building.Create(CampusId.New(), "Academic Building 1", null, DateTimeOffset.UtcNow);
        var room = Room.Create(building.Id, "101", null, null, DateTimeOffset.UtcNow);

        var buildings = new FakeBuildingRepository();
        buildings.Seed(building);
        var rooms = new FakeRoomRepository();
        rooms.Seed(room);

        var service = new BuildingService(buildings, new FakeCampusRepository(), rooms, new FakeUnitOfWork(), new FakeAuditRecorder(), new FakeClock());

        var result = await service.DeleteAsync(building.Id.Value, _audit);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error!.Type);
        Assert.Equal("building.has_rooms", result.Error.Code);
    }

    [Fact]
    public async Task DeleteAsync_succeeds_for_a_Building_with_zero_Rooms()
    {
        var building = Building.Create(CampusId.New(), "Academic Building 1", null, DateTimeOffset.UtcNow);

        var buildings = new FakeBuildingRepository();
        buildings.Seed(building);
        var auditRecorder = new FakeAuditRecorder();

        var service = new BuildingService(buildings, new FakeCampusRepository(), new FakeRoomRepository(), new FakeUnitOfWork(), auditRecorder, new FakeClock());

        var result = await service.DeleteAsync(building.Id.Value, _audit);

        Assert.True(result.IsSuccess);
        Assert.Contains(auditRecorder.RecordedEntries, e => e.Action == "delete" && e.EntityType == "Building");
    }
}

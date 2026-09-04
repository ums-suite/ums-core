using UMS.Modules.Organization.Domain.Campuses;
using UMS.Modules.Organization.Domain.Facilities;

namespace UMS.Modules.Organization.UnitTests.Facilities;

public class BuildingAndRoomTests
{
    private static readonly DateTimeOffset _now = DateTimeOffset.UtcNow;

    [Fact]
    public void Building_Create_with_blank_name_throws()
    {
        Assert.Throws<ArgumentException>(() => Building.Create(CampusId.New(), " ", null, _now));
    }

    [Fact]
    public void Building_Create_links_to_the_given_campus()
    {
        var campusId = CampusId.New();

        var building = Building.Create(campusId, "Academic Building 1", "AB1", _now);

        Assert.Equal(campusId, building.CampusId);
        Assert.Equal("AB1", building.Code);
    }

    [Fact]
    public void Room_Create_with_blank_name_throws()
    {
        Assert.Throws<ArgumentException>(() => Room.Create(BuildingId.New(), " ", null, null, _now));
    }

    [Fact]
    public void Room_Create_with_negative_capacity_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Room.Create(BuildingId.New(), "101", -1, "classroom", _now));
    }

    [Fact]
    public void Room_Create_links_to_the_given_building()
    {
        var buildingId = BuildingId.New();

        var room = Room.Create(buildingId, "101", 60, "classroom", _now);

        Assert.Equal(buildingId, room.BuildingId);
        Assert.Equal(60, room.Capacity);
        Assert.Equal("classroom", room.RoomType);
    }
}

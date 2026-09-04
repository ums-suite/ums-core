using System.Net;
using System.Net.Http.Json;
using UMS.Modules.Organization.Application.Campuses;
using UMS.Modules.Organization.Application.Facilities;
using UMS.Modules.Organization.Application.Universities;
using UMS.Modules.Organization.IntegrationTests.Infrastructure;

namespace UMS.Modules.Organization.IntegrationTests.Facilities;

/// <summary>
/// ORG-7: Building/Room CRUD plus the hard-delete path design-decisions.md re-authorizes for these
/// two levels specifically. The cross-module reference check is exercised only through its stub
/// (always permissive, since Hostel/Academic don't exist yet) - this proves the re-check-before-
/// commit code path runs and a "no references" outcome allows the delete, not that a real
/// cross-module block works (there is nothing real to block against yet).
/// </summary>
[Collection(OrganizationApiTestCollectionDefinition.Name)]
public class FacilitiesTests(OrganizationApiFixture fixture)
{
    [Fact]
    public async Task Building_and_Room_can_be_created_and_the_Room_hard_deleted()
    {
        using var client = fixture.CreateClient();
        var (_, adminUsername, adminPassword, _) = await TestDataSeeder.ProvisionAdminAsync(fixture, client);
        var admin = await TestUsers.LoginAsync(client, adminUsername, adminPassword);

        var university = await CreateAsync<UniversityDto>(client, admin.AccessToken, "/api/v1/organization/universities", new CreateUniversityRequest($"University {Guid.NewGuid():N}", null));
        var campus = await CreateAsync<CampusDto>(client, admin.AccessToken, "/api/v1/organization/campuses", new CreateCampusRequest(university.Id, "Main Campus"));
        var building = await CreateAsync<BuildingDto>(client, admin.AccessToken, "/api/v1/organization/buildings", new CreateBuildingRequest(campus.Id, "Academic Building 1", "AB1"));
        var room = await CreateAsync<RoomDto>(client, admin.AccessToken, "/api/v1/organization/rooms", new CreateRoomRequest(building.Id, "101", 60, "classroom"));

        Assert.Equal(building.Id, room.BuildingId);

        var roomsUnderBuilding = await client.GetFromJsonAsync<RoomListPage>($"/api/v1/organization/buildings/{building.Id}/rooms");
        Assert.Contains(roomsUnderBuilding!.Items, r => r.Id == room.Id);

        var deleteRoom = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/organization/rooms/{room.Id}").WithBearerToken(admin.AccessToken));
        Assert.Equal(HttpStatusCode.NoContent, deleteRoom.StatusCode);

        var getDeletedRoom = await client.GetAsync($"/api/v1/organization/rooms/{room.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getDeletedRoom.StatusCode);
    }

    [Fact]
    public async Task Hard_deleting_a_Building_with_Rooms_still_attached_is_rejected()
    {
        using var client = fixture.CreateClient();
        var (_, adminUsername, adminPassword, _) = await TestDataSeeder.ProvisionAdminAsync(fixture, client);
        var admin = await TestUsers.LoginAsync(client, adminUsername, adminPassword);

        var university = await CreateAsync<UniversityDto>(client, admin.AccessToken, "/api/v1/organization/universities", new CreateUniversityRequest($"University {Guid.NewGuid():N}", null));
        var campus = await CreateAsync<CampusDto>(client, admin.AccessToken, "/api/v1/organization/campuses", new CreateCampusRequest(university.Id, "Main Campus"));
        var building = await CreateAsync<BuildingDto>(client, admin.AccessToken, "/api/v1/organization/buildings", new CreateBuildingRequest(campus.Id, "Academic Building 2", null));
        await CreateAsync<RoomDto>(client, admin.AccessToken, "/api/v1/organization/rooms", new CreateRoomRequest(building.Id, "201", null, null));

        var deleteBuilding = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/organization/buildings/{building.Id}").WithBearerToken(admin.AccessToken));

        Assert.Equal(HttpStatusCode.Conflict, deleteBuilding.StatusCode);
    }

    [Fact]
    public async Task Room_capacity_cannot_be_negative()
    {
        using var client = fixture.CreateClient();
        var (_, adminUsername, adminPassword, _) = await TestDataSeeder.ProvisionAdminAsync(fixture, client);
        var admin = await TestUsers.LoginAsync(client, adminUsername, adminPassword);

        var university = await CreateAsync<UniversityDto>(client, admin.AccessToken, "/api/v1/organization/universities", new CreateUniversityRequest($"University {Guid.NewGuid():N}", null));
        var campus = await CreateAsync<CampusDto>(client, admin.AccessToken, "/api/v1/organization/campuses", new CreateCampusRequest(university.Id, "Main Campus"));
        var building = await CreateAsync<BuildingDto>(client, admin.AccessToken, "/api/v1/organization/buildings", new CreateBuildingRequest(campus.Id, "Academic Building 3", null));

        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/v1/organization/rooms")
        {
            Content = JsonContent.Create(new CreateRoomRequest(building.Id, "301", -5, null)),
        }.WithBearerToken(admin.AccessToken));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<T> CreateAsync<T>(HttpClient client, string accessToken, string path, object body)
    {
        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body),
        }.WithBearerToken(accessToken));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}

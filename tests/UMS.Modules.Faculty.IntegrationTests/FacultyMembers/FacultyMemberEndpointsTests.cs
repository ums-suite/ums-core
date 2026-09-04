using System.Net;
using System.Net.Http.Json;
using UMS.Modules.Faculty.Application.FacultyMembers;
using UMS.Modules.Faculty.IntegrationTests.Infrastructure;

namespace UMS.Modules.Faculty.IntegrationTests.FacultyMembers;

[Collection(FacultyApiTestCollectionDefinition.Name)]
public sealed class FacultyMemberEndpointsTests(FacultyApiFixture fixture)
{
    [Fact]
    public async Task Onboard_then_get_returns_the_same_profile()
    {
        var client = fixture.CreateClient();
        var (_, _, _, adminToken) = await FacultyTestDataSeeder.ProvisionAdminAsync(fixture, client);
        var departmentId = await FacultyTestDataSeeder.SeedDepartmentAsync(client, adminToken);
        var designationId = await FacultyTestDataSeeder.SeedDesignationAsync(client, adminToken);
        var member = await TestUsers.ProvisionAsync(client);

        var onboardRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/faculty/members")
        {
            Content = JsonContent.Create(new OnboardFacultyMemberRequest(member.Id, $"EMP-{Guid.NewGuid():N}"[..12], departmentId, designationId, "FullTime", new DateOnly(2020, 1, 1))),
        }.WithBearerToken(adminToken);
        var onboardResponse = await client.SendAsync(onboardRequest);
        Assert.Equal(HttpStatusCode.Created, onboardResponse.StatusCode);
        var created = await onboardResponse.Content.ReadFromJsonAsync<FacultyMemberDto>();

        var getRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/faculty/members/{created!.Id}").WithBearerToken(adminToken);
        var getResponse = await client.SendAsync(getRequest);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var fetched = await getResponse.Content.ReadFromJsonAsync<FacultyMemberDto>();

        Assert.Equal(created.Id, fetched!.Id);
        Assert.Equal(departmentId, fetched.DepartmentId);
        Assert.Equal("Active", fetched.Status);
    }

    [Fact]
    public async Task Onboard_with_nonexistent_department_is_rejected()
    {
        var client = fixture.CreateClient();
        var (_, _, _, adminToken) = await FacultyTestDataSeeder.ProvisionAdminAsync(fixture, client);
        var member = await TestUsers.ProvisionAsync(client);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/faculty/members")
        {
            Content = JsonContent.Create(new OnboardFacultyMemberRequest(member.Id, $"EMP-{Guid.NewGuid():N}"[..12], Guid.NewGuid(), Guid.NewGuid(), "FullTime", new DateOnly(2020, 1, 1))),
        }.WithBearerToken(adminToken);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Self_service_update_by_the_owning_member_succeeds_but_by_someone_else_is_forbidden()
    {
        var client = fixture.CreateClient();
        var (_, _, _, adminToken) = await FacultyTestDataSeeder.ProvisionAdminAsync(fixture, client);
        var departmentId = await FacultyTestDataSeeder.SeedDepartmentAsync(client, adminToken);
        var designationId = await FacultyTestDataSeeder.SeedDesignationAsync(client, adminToken);
        var member = await TestUsers.ProvisionAsync(client);
        var memberLogin = await TestUsers.LoginAsync(client, member.Username);

        var onboardRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/faculty/members")
        {
            Content = JsonContent.Create(new OnboardFacultyMemberRequest(member.Id, $"EMP-{Guid.NewGuid():N}"[..12], departmentId, designationId, "FullTime", new DateOnly(2020, 1, 1))),
        }.WithBearerToken(adminToken);
        var onboarded = await (await client.SendAsync(onboardRequest)).Content.ReadFromJsonAsync<FacultyMemberDto>();

        // The owning member may update their own bounded self-service fields.
        var selfServiceRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/faculty/members/{onboarded!.Id}/self-service")
        {
            Content = JsonContent.Create(new UpdateSelfServiceProfileRequest("me@example.edu.bd", "+8801712345678", onboarded.Version)),
        }.WithBearerToken(memberLogin.AccessToken);
        var selfServiceResponse = await client.SendAsync(selfServiceRequest);
        Assert.Equal(HttpStatusCode.OK, selfServiceResponse.StatusCode);
        var updated = await selfServiceResponse.Content.ReadFromJsonAsync<FacultyMemberDto>();
        Assert.Equal("me@example.edu.bd", updated!.ContactEmail);

        // A different, unrelated caller may not.
        var otherUser = await TestUsers.ProvisionAsync(client);
        var otherLogin = await TestUsers.LoginAsync(client, otherUser.Username);
        var forbiddenRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/faculty/members/{onboarded.Id}/self-service")
        {
            Content = JsonContent.Create(new UpdateSelfServiceProfileRequest("hacker@example.edu.bd", null, updated.Version)),
        }.WithBearerToken(otherLogin.AccessToken);
        var forbiddenResponse = await client.SendAsync(forbiddenRequest);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);
    }

    [Fact]
    public async Task Stale_version_on_employment_details_update_returns_conflict()
    {
        var client = fixture.CreateClient();
        var (_, _, _, adminToken) = await FacultyTestDataSeeder.ProvisionAdminAsync(fixture, client);
        var departmentId = await FacultyTestDataSeeder.SeedDepartmentAsync(client, adminToken);
        var designationId = await FacultyTestDataSeeder.SeedDesignationAsync(client, adminToken);
        var member = await TestUsers.ProvisionAsync(client);

        var onboardRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/faculty/members")
        {
            Content = JsonContent.Create(new OnboardFacultyMemberRequest(member.Id, $"EMP-{Guid.NewGuid():N}"[..12], departmentId, designationId, "FullTime", new DateOnly(2020, 1, 1))),
        }.WithBearerToken(adminToken);
        var onboarded = await (await client.SendAsync(onboardRequest)).Content.ReadFromJsonAsync<FacultyMemberDto>();

        // Win the race first.
        var firstUpdate = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/faculty/members/{onboarded!.Id}")
        {
            Content = JsonContent.Create(new UpdateEmploymentDetailsRequest(departmentId, designationId, "PartTime", false, null, null, onboarded.Version)),
        }.WithBearerToken(adminToken);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(firstUpdate)).StatusCode);

        // Retry with the now-stale version.
        var staleUpdate = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/faculty/members/{onboarded.Id}")
        {
            Content = JsonContent.Create(new UpdateEmploymentDetailsRequest(departmentId, designationId, "Adjunct", false, null, null, onboarded.Version)),
        }.WithBearerToken(adminToken);
        var staleResponse = await client.SendAsync(staleUpdate);

        Assert.Equal(HttpStatusCode.Conflict, staleResponse.StatusCode);
    }
}

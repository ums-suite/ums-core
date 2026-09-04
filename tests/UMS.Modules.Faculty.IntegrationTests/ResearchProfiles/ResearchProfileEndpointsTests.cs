using System.Net;
using System.Net.Http.Json;
using UMS.Modules.Faculty.Application.FacultyMembers;
using UMS.Modules.Faculty.Application.ResearchProfiles;
using UMS.Modules.Faculty.IntegrationTests.Infrastructure;

namespace UMS.Modules.Faculty.IntegrationTests.ResearchProfiles;

[Collection(FacultyApiTestCollectionDefinition.Name)]
public sealed class ResearchProfileEndpointsTests(FacultyApiFixture fixture)
{
    [Fact]
    public async Task Owning_member_can_publish_their_own_profile_and_it_is_publicly_readable()
    {
        var client = fixture.CreateClient();
        var (_, _, _, adminToken) = await FacultyTestDataSeeder.ProvisionAdminAsync(fixture, client);
        var departmentId = await FacultyTestDataSeeder.SeedDepartmentAsync(client, adminToken);
        var designationId = await FacultyTestDataSeeder.SeedDesignationAsync(client, adminToken);

        var memberUser = await TestUsers.ProvisionAsync(client);
        await FacultyTestDataSeeder.GrantFacultyMemberRoleAsync(fixture, memberUser.Id);
        var memberLogin = await TestUsers.LoginAsync(client, memberUser.Username);
        var onboardRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/faculty/members")
        {
            Content = JsonContent.Create(new OnboardFacultyMemberRequest(memberUser.Id, $"EMP-{Guid.NewGuid():N}"[..12], departmentId, designationId, "FullTime", new DateOnly(2020, 1, 1))),
        }.WithBearerToken(adminToken);
        var facultyMember = await (await client.SendAsync(onboardRequest)).Content.ReadFromJsonAsync<FacultyMemberDto>();

        var publications = new[] { new PublicationDto("A Study of Testing", "Journal of Integration Tests", 2025, "https://example.org/paper") };
        var updateRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/faculty/members/{facultyMember!.Id}/research-profile")
        {
            Content = JsonContent.Create(new UpdateResearchProfileRequest(publications, "Studying eventual consistency", null, 0)),
        }.WithBearerToken(memberLogin.AccessToken);
        var updateResponse = await client.SendAsync(updateRequest);

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<ResearchProfileDto>();
        Assert.Single(updated!.Publications);

        // Public read - no auth header at all.
        var publicReadResponse = await client.GetAsync($"/api/v1/faculty/members/{facultyMember.Id}/research-profile");
        Assert.Equal(HttpStatusCode.OK, publicReadResponse.StatusCode);
        var publiclyRead = await publicReadResponse.Content.ReadFromJsonAsync<ResearchProfileDto>();
        Assert.Equal("A Study of Testing", publiclyRead!.Publications[0].Title);
    }

    [Fact]
    public async Task A_non_owning_non_HR_caller_cannot_write_someone_elses_profile()
    {
        var client = fixture.CreateClient();
        var (_, _, _, adminToken) = await FacultyTestDataSeeder.ProvisionAdminAsync(fixture, client);
        var departmentId = await FacultyTestDataSeeder.SeedDepartmentAsync(client, adminToken);
        var designationId = await FacultyTestDataSeeder.SeedDesignationAsync(client, adminToken);

        var memberUser = await TestUsers.ProvisionAsync(client);
        var onboardRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/faculty/members")
        {
            Content = JsonContent.Create(new OnboardFacultyMemberRequest(memberUser.Id, $"EMP-{Guid.NewGuid():N}"[..12], departmentId, designationId, "FullTime", new DateOnly(2020, 1, 1))),
        }.WithBearerToken(adminToken);
        var facultyMember = await (await client.SendAsync(onboardRequest)).Content.ReadFromJsonAsync<FacultyMemberDto>();

        var strangerUser = await TestUsers.ProvisionAsync(client);
        var strangerLogin = await TestUsers.LoginAsync(client, strangerUser.Username);

        var updateRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/faculty/members/{facultyMember!.Id}/research-profile")
        {
            Content = JsonContent.Create(new UpdateResearchProfileRequest([], "Should not be allowed", null, 0)),
        }.WithBearerToken(strangerLogin.AccessToken);
        var response = await client.SendAsync(updateRequest);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}

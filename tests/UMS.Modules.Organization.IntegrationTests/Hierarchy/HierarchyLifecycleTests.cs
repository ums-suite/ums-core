using System.Net;
using System.Net.Http.Json;
using UMS.Modules.Organization.Application.Campuses;
using UMS.Modules.Organization.Application.Departments;
using UMS.Modules.Organization.Application.Faculties;
using UMS.Modules.Organization.Application.Universities;
using UMS.Modules.Organization.Api.Contracts;
using UMS.Modules.Organization.IntegrationTests.Infrastructure;

namespace UMS.Modules.Organization.IntegrationTests.Hierarchy;

/// <summary>
/// ORG-1..ORG-5's full golden path plus the edge-cases.md scenarios exercisable without genuine
/// concurrent timing: parent-active-status validation (orphan prevention), optimistic-concurrency
/// `409` on a stale `PATCH`, the DB-level `(parent_id, name)` uniqueness constraint, and
/// deactivation cascade-blocking. True concurrent-timing races (two simultaneous requests racing
/// the same row lock) are NOT exercised here - see the PR description for what was verified
/// manually instead.
/// </summary>
[Collection(OrganizationApiTestCollectionDefinition.Name)]
public class HierarchyLifecycleTests(OrganizationApiFixture fixture)
{
    [Fact]
    public async Task Full_hierarchy_can_be_created_top_to_bottom_and_deactivated_bottom_to_top()
    {
        using var client = fixture.CreateClient();
        var (_, adminUsername, adminPassword, _) = await TestDataSeeder.ProvisionAdminAsync(fixture, client);
        var admin = await TestUsers.LoginAsync(client, adminUsername, adminPassword);

        var university = await CreateAsync<UniversityDto>(client, admin.AccessToken, "/api/v1/organization/universities", new CreateUniversityRequest($"University {Guid.NewGuid():N}", "UNI"));
        var campus = await CreateAsync<CampusDto>(client, admin.AccessToken, "/api/v1/organization/campuses", new CreateCampusRequest(university.Id, "Main Campus"));
        var faculty = await CreateAsync<FacultyDto>(client, admin.AccessToken, "/api/v1/organization/faculties", new CreateFacultyRequest(campus.Id, "Faculty of Engineering", null));
        var department = await CreateAsync<DepartmentDto>(client, admin.AccessToken, "/api/v1/organization/departments", new CreateDepartmentRequest(faculty.Id, "Computer Science", null));

        Assert.Equal(university.Id, campus.UniversityId);
        Assert.Equal(campus.Id, faculty.CampusId);
        Assert.Equal(faculty.Id, department.FacultyId);
        Assert.Equal("Active", department.Status);

        // requirement-spec.md organization §4 "Deactivation cascades are blocked, not silent" -
        // the Faculty still has an active Department beneath it.
        var blockedDeactivate = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"/api/v1/organization/faculties/{faculty.Id}/deactivate")
        {
            Content = JsonContent.Create(new DeactivateRequestBody(faculty.Version)),
        }.WithBearerToken(admin.AccessToken));
        Assert.Equal(HttpStatusCode.Conflict, blockedDeactivate.StatusCode);

        // Deactivate bottom-up: Department first, then Faculty succeeds.
        var deactivateDepartment = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"/api/v1/organization/departments/{department.Id}/deactivate")
        {
            Content = JsonContent.Create(new DeactivateRequestBody(department.Version)),
        }.WithBearerToken(admin.AccessToken));
        Assert.Equal(HttpStatusCode.OK, deactivateDepartment.StatusCode);

        var deactivateFaculty = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"/api/v1/organization/faculties/{faculty.Id}/deactivate")
        {
            Content = JsonContent.Create(new DeactivateRequestBody(faculty.Version)),
        }.WithBearerToken(admin.AccessToken));
        Assert.Equal(HttpStatusCode.OK, deactivateFaculty.StatusCode);
    }

    [Fact]
    public async Task Creating_a_Campus_under_an_unknown_University_returns_NotFound()
    {
        using var client = fixture.CreateClient();
        var (_, adminUsername, adminPassword, _) = await TestDataSeeder.ProvisionAdminAsync(fixture, client);
        var admin = await TestUsers.LoginAsync(client, adminUsername, adminPassword);

        var response = await client.PostAsJsonAsync("/api/v1/organization/campuses", new CreateCampusRequest(Guid.NewGuid(), "Main Campus"), CancellationToken.None);
        var withAuth = new HttpRequestMessage(HttpMethod.Post, "/api/v1/organization/campuses")
        {
            Content = JsonContent.Create(new CreateCampusRequest(Guid.NewGuid(), "Main Campus")),
        }.WithBearerToken(admin.AccessToken);
        var authedResponse = await client.SendAsync(withAuth);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, authedResponse.StatusCode);
    }

    [Fact]
    public async Task Creating_a_Faculty_under_an_inactive_Campus_is_rejected()
    {
        using var client = fixture.CreateClient();
        var (_, adminUsername, adminPassword, _) = await TestDataSeeder.ProvisionAdminAsync(fixture, client);
        var admin = await TestUsers.LoginAsync(client, adminUsername, adminPassword);

        var university = await CreateAsync<UniversityDto>(client, admin.AccessToken, "/api/v1/organization/universities", new CreateUniversityRequest($"University {Guid.NewGuid():N}", null));
        var campus = await CreateAsync<CampusDto>(client, admin.AccessToken, "/api/v1/organization/campuses", new CreateCampusRequest(university.Id, "Main Campus"));

        var deactivateCampus = await client.SendAsync(new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/organization/campuses/{campus.Id}")
        {
            Content = JsonContent.Create(new UpdateCampusRequest(null, "Inactive", campus.Version)),
        }.WithBearerToken(admin.AccessToken));
        Assert.Equal(HttpStatusCode.OK, deactivateCampus.StatusCode);

        var createFaculty = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/v1/organization/faculties")
        {
            Content = JsonContent.Create(new CreateFacultyRequest(campus.Id, "Faculty of Engineering", null)),
        }.WithBearerToken(admin.AccessToken));

        Assert.Equal(HttpStatusCode.BadRequest, createFaculty.StatusCode);
    }

    [Fact]
    public async Task Concurrent_edit_conflict_surfaces_as_409_on_a_stale_PATCH()
    {
        // edge-cases.md "Concurrent edits to the same Department/Program (lost update)" - the
        // sequential form: PATCH once (advancing the version), then PATCH again with the
        // now-stale version the client originally read.
        using var client = fixture.CreateClient();
        var (_, adminUsername, adminPassword, _) = await TestDataSeeder.ProvisionAdminAsync(fixture, client);
        var admin = await TestUsers.LoginAsync(client, adminUsername, adminPassword);

        var university = await CreateAsync<UniversityDto>(client, admin.AccessToken, "/api/v1/organization/universities", new CreateUniversityRequest($"University {Guid.NewGuid():N}", null));
        var campus = await CreateAsync<CampusDto>(client, admin.AccessToken, "/api/v1/organization/campuses", new CreateCampusRequest(university.Id, "Main Campus"));
        var faculty = await CreateAsync<FacultyDto>(client, admin.AccessToken, "/api/v1/organization/faculties", new CreateFacultyRequest(campus.Id, "Faculty of Engineering", null));

        var firstEdit = await client.SendAsync(new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/organization/faculties/{faculty.Id}")
        {
            Content = JsonContent.Create(new UpdateFacultyRequest("Faculty of Engineering (Renamed)", faculty.Version, null)),
        }.WithBearerToken(admin.AccessToken));
        Assert.Equal(HttpStatusCode.OK, firstEdit.StatusCode);

        // Second admin's session still holds the ORIGINAL (now-stale) version.
        var staleEdit = await client.SendAsync(new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/organization/faculties/{faculty.Id}")
        {
            Content = JsonContent.Create(new UpdateFacultyRequest("Faculty of Engineering (Conflicting Edit)", faculty.Version, null)),
        }.WithBearerToken(admin.AccessToken));

        Assert.Equal(HttpStatusCode.Conflict, staleEdit.StatusCode);
    }

    [Fact]
    public async Task Duplicate_Campus_name_under_the_same_University_is_rejected_by_the_DB_constraint()
    {
        // edge-cases.md "Two Departments with the identical name created simultaneously under the
        // same Faculty" - the sequential form at the Campus/University level: the DB-level
        // composite unique constraint, not an application-level check, is what actually rejects
        // the second insert.
        using var client = fixture.CreateClient();
        var (_, adminUsername, adminPassword, _) = await TestDataSeeder.ProvisionAdminAsync(fixture, client);
        var admin = await TestUsers.LoginAsync(client, adminUsername, adminPassword);

        var university = await CreateAsync<UniversityDto>(client, admin.AccessToken, "/api/v1/organization/universities", new CreateUniversityRequest($"University {Guid.NewGuid():N}", null));
        await CreateAsync<CampusDto>(client, admin.AccessToken, "/api/v1/organization/campuses", new CreateCampusRequest(university.Id, "Duplicate Campus"));

        var duplicate = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/v1/organization/campuses")
        {
            Content = JsonContent.Create(new CreateCampusRequest(university.Id, "Duplicate Campus")),
        }.WithBearerToken(admin.AccessToken));

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task Organization_reads_are_anonymous_while_writes_require_the_manage_permission()
    {
        using var client = fixture.CreateClient();

        var anonymousRead = await client.GetAsync("/api/v1/organization/universities");
        Assert.Equal(HttpStatusCode.OK, anonymousRead.StatusCode);

        var anonymousWrite = await client.PostAsJsonAsync("/api/v1/organization/universities", new CreateUniversityRequest("Should Fail", null));
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousWrite.StatusCode);
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

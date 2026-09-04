using System.Net.Http.Json;
using UMS.Modules.Organization.Application.Campuses;
using UMS.Modules.Organization.Application.Departments;
using UMS.Modules.Organization.Application.Faculties;
using UMS.Modules.Organization.Application.Hierarchy;
using UMS.Modules.Organization.Application.Universities;
using UMS.Modules.Organization.IntegrationTests.Infrastructure;

namespace UMS.Modules.Organization.IntegrationTests.Hierarchy;

/// <summary>ORG-9/ORG-10: `GET /organization-tree` (full or subtree) and `GET /nodes/{id}/ancestors`, including that a write actually invalidates the cache rather than serving a stale tree.</summary>
[Collection(OrganizationApiTestCollectionDefinition.Name)]
public class HierarchyQueryTests(OrganizationApiFixture fixture)
{
    [Fact]
    public async Task Organization_tree_and_ancestors_reflect_the_created_hierarchy()
    {
        using var client = fixture.CreateClient();
        var (_, adminUsername, adminPassword, _) = await TestDataSeeder.ProvisionAdminAsync(fixture, client);
        var admin = await TestUsers.LoginAsync(client, adminUsername, adminPassword);

        var university = await CreateAsync<UniversityDto>(client, admin.AccessToken, "/api/v1/organization/universities", new CreateUniversityRequest($"University {Guid.NewGuid():N}", null));
        var campus = await CreateAsync<CampusDto>(client, admin.AccessToken, "/api/v1/organization/campuses", new CreateCampusRequest(university.Id, "Main Campus"));
        var faculty = await CreateAsync<FacultyDto>(client, admin.AccessToken, "/api/v1/organization/faculties", new CreateFacultyRequest(campus.Id, "Faculty of Engineering", null));
        var department = await CreateAsync<DepartmentDto>(client, admin.AccessToken, "/api/v1/organization/departments", new CreateDepartmentRequest(faculty.Id, "Computer Science", null));

        var subtree = await client.GetFromJsonAsync<List<OrganizationTreeNodeDto>>($"/api/v1/organization/organization-tree?rootId={campus.Id}");
        var campusNode = Assert.Single(subtree!);
        Assert.Equal(campus.Id, campusNode.Id);
        var facultyNode = Assert.Single(campusNode.Children);
        Assert.Equal(faculty.Id, facultyNode.Id);
        var departmentNode = Assert.Single(facultyNode.Children);
        Assert.Equal(department.Id, departmentNode.Id);

        var ancestors = await client.GetFromJsonAsync<List<AncestorNodeDto>>($"/api/v1/organization/nodes/{department.Id}/ancestors");
        Assert.Equal(4, ancestors!.Count);
        Assert.Equal(new[] { university.Id, campus.Id, faculty.Id, department.Id }, ancestors.Select(a => a.Id));
    }

    [Fact]
    public async Task Renaming_a_Faculty_invalidates_its_cached_subtree()
    {
        using var client = fixture.CreateClient();
        var (_, adminUsername, adminPassword, _) = await TestDataSeeder.ProvisionAdminAsync(fixture, client);
        var admin = await TestUsers.LoginAsync(client, adminUsername, adminPassword);

        var university = await CreateAsync<UniversityDto>(client, admin.AccessToken, "/api/v1/organization/universities", new CreateUniversityRequest($"University {Guid.NewGuid():N}", null));
        var campus = await CreateAsync<CampusDto>(client, admin.AccessToken, "/api/v1/organization/campuses", new CreateCampusRequest(university.Id, "Main Campus"));
        var faculty = await CreateAsync<FacultyDto>(client, admin.AccessToken, "/api/v1/organization/faculties", new CreateFacultyRequest(campus.Id, "Original Name", null));

        // Warms the cache (design-decisions.md, "Caching Strategy for the Hierarchy Tree").
        _ = await client.GetFromJsonAsync<List<OrganizationTreeNodeDto>>($"/api/v1/organization/organization-tree?rootId={faculty.Id}");

        var rename = await client.SendAsync(new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/organization/faculties/{faculty.Id}")
        {
            Content = JsonContent.Create(new UpdateFacultyRequest("Renamed Faculty", faculty.Version, null)),
        }.WithBearerToken(admin.AccessToken));
        rename.EnsureSuccessStatusCode();

        var afterRename = await client.GetFromJsonAsync<List<OrganizationTreeNodeDto>>($"/api/v1/organization/organization-tree?rootId={faculty.Id}");
        var facultyNode = Assert.Single(afterRename!);
        Assert.Equal("Renamed Faculty", facultyNode.Name);
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

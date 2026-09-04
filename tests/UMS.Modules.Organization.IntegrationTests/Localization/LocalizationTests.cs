using System.Net.Http.Json;
using UMS.Modules.Organization.Application.Campuses;
using UMS.Modules.Organization.Application.Designations;
using UMS.Modules.Organization.Application.Faculties;
using UMS.Modules.Organization.Application.Universities;
using UMS.Modules.Organization.IntegrationTests.Infrastructure;

namespace UMS.Modules.Organization.IntegrationTests.Localization;

/// <summary>ORG-8/ADR-0011: `{table}_translations`, English-fallback resolution server-side via `?lang=`.</summary>
[Collection(OrganizationApiTestCollectionDefinition.Name)]
public class LocalizationTests(OrganizationApiFixture fixture)
{
    [Fact]
    public async Task Faculty_name_resolves_to_the_translation_when_lang_is_requested_and_falls_back_to_English_otherwise()
    {
        using var client = fixture.CreateClient();
        var (_, adminUsername, adminPassword, _) = await TestDataSeeder.ProvisionAdminAsync(fixture, client);
        var admin = await TestUsers.LoginAsync(client, adminUsername, adminPassword);

        var university = await CreateAsync<UniversityDto>(client, admin.AccessToken, "/api/v1/organization/universities", new CreateUniversityRequest($"University {Guid.NewGuid():N}", null));
        var campus = await CreateAsync<CampusDto>(client, admin.AccessToken, "/api/v1/organization/campuses", new CreateCampusRequest(university.Id, "Main Campus"));

        var translations = new Dictionary<string, string> { ["bn"] = "প্রকৌশল অনুষদ" };
        var faculty = await CreateAsync<FacultyDto>(client, admin.AccessToken, "/api/v1/organization/faculties", new CreateFacultyRequest(campus.Id, "Faculty of Engineering", translations));

        var englishRead = await client.GetFromJsonAsync<FacultyDto>($"/api/v1/organization/faculties/{faculty.Id}");
        var bengaliRead = await client.GetFromJsonAsync<FacultyDto>($"/api/v1/organization/faculties/{faculty.Id}?lang=bn");
        var unknownLanguageRead = await client.GetFromJsonAsync<FacultyDto>($"/api/v1/organization/faculties/{faculty.Id}?lang=fr");

        Assert.Equal("Faculty of Engineering", englishRead!.LocalizedName);
        Assert.Equal("প্রকৌশল অনুষদ", bengaliRead!.LocalizedName);
        Assert.Equal("Faculty of Engineering", unknownLanguageRead!.LocalizedName);
        Assert.Equal("Faculty of Engineering", faculty.Name);
    }

    [Fact]
    public async Task Designation_title_translation_round_trips()
    {
        using var client = fixture.CreateClient();
        var (_, adminUsername, adminPassword, _) = await TestDataSeeder.ProvisionAdminAsync(fixture, client);
        var admin = await TestUsers.LoginAsync(client, adminUsername, adminPassword);

        var translations = new Dictionary<string, string> { ["bn"] = "অধ্যাপক" };
        var designation = await CreateAsync<DesignationDto>(client, admin.AccessToken, "/api/v1/organization/designations", new CreateDesignationRequest($"Professor {Guid.NewGuid():N}", translations));

        var bengaliRead = await client.GetFromJsonAsync<DesignationDto>($"/api/v1/organization/designations/{designation.Id}?lang=bn");

        Assert.Equal("অধ্যাপক", bengaliRead!.LocalizedTitle);
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

using System.Net;
using System.Net.Http.Json;
using UMS.Modules.Student.Application.Students;
using UMS.Modules.Student.IntegrationTests.Infrastructure;

namespace UMS.Modules.Student.IntegrationTests.Students;

/// <summary>STU-5/STU-6/STU-7 (requirement-spec.md student §6).</summary>
[Collection(StudentApiTestCollectionDefinition.Name)]
public sealed class StudentProfileEndpointsTests(StudentApiFixture fixture)
{
    [Fact]
    public async Task Get_me_returns_the_caller_own_Student_profile()
    {
        var client = fixture.CreateClient();
        var (_, _, _, adminToken) = await StudentTestDataSeeder.ProvisionAdminAsync(fixture, client);
        var departmentId = await StudentTestDataSeeder.SeedDepartmentAsync(client, adminToken);

        var created = await StudentTestDataSeeder.CreateStudentRecordAsync(fixture, departmentId);
        Assert.NotNull(created.IdentityUserId);

        var login = await StudentTestDataSeeder.ResetPasswordAndLoginAsync(fixture, client, created.IdentityUserId!.Value, "a-Kn0wn-Passw0rd!");

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/student/students/me").WithBearerToken(login.AccessToken);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<StudentDto>();
        Assert.Equal(created.StudentId, dto!.Id);
        Assert.Equal(created.StudentNumber, dto.StudentNumber);
    }

    [Fact]
    public async Task Put_me_updates_contact_info_and_photo_but_never_identity_bearing_fields()
    {
        var client = fixture.CreateClient();
        var (_, _, _, adminToken) = await StudentTestDataSeeder.ProvisionAdminAsync(fixture, client);
        var departmentId = await StudentTestDataSeeder.SeedDepartmentAsync(client, adminToken);

        var created = await StudentTestDataSeeder.CreateStudentRecordAsync(fixture, departmentId);
        var login = await StudentTestDataSeeder.ResetPasswordAndLoginAsync(fixture, client, created.IdentityUserId!.Value, "a-Kn0wn-Passw0rd!");

        var getRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/student/students/me").WithBearerToken(login.AccessToken);
        var before = await (await client.SendAsync(getRequest)).Content.ReadFromJsonAsync<StudentDto>();

        var putRequest = new HttpRequestMessage(HttpMethod.Put, "/api/v1/student/students/me")
        {
            Content = JsonContent.Create(new UpdateSelfServiceProfileRequest("new-contact@example.edu.bd", "+8801912345678", "https://example.com/photo.jpg", before!.Version)),
        }.WithBearerToken(login.AccessToken);
        var putResponse = await client.SendAsync(putRequest);

        Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);
        var after = await putResponse.Content.ReadFromJsonAsync<StudentDto>();
        Assert.Equal("new-contact@example.edu.bd", after!.ContactEmail);
        Assert.Equal("+8801912345678", after.ContactPhone);
        Assert.Equal("https://example.com/photo.jpg", after.PhotoUrl);

        // Identity-bearing fields must be exactly unchanged - UpdateSelfServiceProfileRequest has
        // no field for them at all (requirement-spec.md §2/§9 decision 2).
        Assert.Equal(before.GivenName, after.GivenName);
        Assert.Equal(before.FamilyName, after.FamilyName);
        Assert.Equal(before.DateOfBirth, after.DateOfBirth);
        Assert.Equal(before.NationalId, after.NationalId);
    }

    [Fact]
    public async Task Get_me_without_a_linked_Student_profile_returns_NotFound()
    {
        var client = fixture.CreateClient();
        var plainUser = await TestUsers.ProvisionAsync(client);
        var login = await TestUsers.LoginAsync(client, plainUser.Username);

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/student/students/me").WithBearerToken(login.AccessToken);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_by_id_requires_the_ProfileRead_permission()
    {
        var client = fixture.CreateClient();
        var (_, _, _, adminToken) = await StudentTestDataSeeder.ProvisionAdminAsync(fixture, client);
        var departmentId = await StudentTestDataSeeder.SeedDepartmentAsync(client, adminToken);
        var created = await StudentTestDataSeeder.CreateStudentRecordAsync(fixture, departmentId);

        var plainUser = await TestUsers.ProvisionAsync(client);
        var plainLogin = await TestUsers.LoginAsync(client, plainUser.Username);
        var unauthorizedRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/student/students/{created.StudentId}").WithBearerToken(plainLogin.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(unauthorizedRequest)).StatusCode);

        var adminRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/student/students/{created.StudentId}").WithBearerToken(adminToken);
        var adminResponse = await client.SendAsync(adminRequest);
        Assert.Equal(HttpStatusCode.OK, adminResponse.StatusCode);
        var dto = await adminResponse.Content.ReadFromJsonAsync<StudentDto>();
        Assert.Equal(created.StudentId, dto!.Id);
    }
}

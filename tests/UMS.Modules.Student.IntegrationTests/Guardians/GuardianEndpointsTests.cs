using System.Net;
using System.Net.Http.Json;
using UMS.Modules.Student.Application.Guardians;
using UMS.Modules.Student.IntegrationTests.Infrastructure;

namespace UMS.Modules.Student.IntegrationTests.Guardians;

/// <summary>Guardian/GuardianAccessGrant scaffolding (docs/ddd/ubiquitous-language.md) - end-to-end through the real HTTP surface.</summary>
[Collection(StudentApiTestCollectionDefinition.Name)]
public sealed class GuardianEndpointsTests(StudentApiFixture fixture)
{
    [Fact]
    public async Task Link_a_Guardian_then_grant_and_revoke_category_scoped_access_no_default_visibility()
    {
        var client = fixture.CreateClient();
        var (_, _, _, adminToken) = await StudentTestDataSeeder.ProvisionAdminAsync(fixture, client);
        var departmentId = await StudentTestDataSeeder.SeedDepartmentAsync(client, adminToken);
        var created = await StudentTestDataSeeder.CreateStudentRecordAsync(fixture, departmentId);
        var login = await StudentTestDataSeeder.ResetPasswordAndLoginAsync(fixture, client, created.IdentityUserId!.Value, "a-Kn0wn-Passw0rd!");

        var linkRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/student/students/me/guardians")
        {
            Content = JsonContent.Create(new LinkGuardianRequest("Karim Uddin", "Father", "karim@example.edu.bd", null)),
        }.WithBearerToken(login.AccessToken);
        var linkResponse = await client.SendAsync(linkRequest);
        Assert.Equal(HttpStatusCode.Created, linkResponse.StatusCode);
        var guardian = await linkResponse.Content.ReadFromJsonAsync<GuardianDto>();
        Assert.Empty(guardian!.ActiveAccessGrants);

        // No default/implicit visibility - a freshly linked Guardian has zero access categories.
        var listRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/student/students/me/guardians").WithBearerToken(login.AccessToken);
        var listedBeforeGrant = await (await client.SendAsync(listRequest)).Content.ReadFromJsonAsync<List<GuardianDto>>();
        Assert.Empty(listedBeforeGrant!.Single().ActiveAccessGrants);

        var grantRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/student/students/me/guardians/{guardian.Id}/access-grants")
        {
            Content = JsonContent.Create(new GrantGuardianAccessRequest("Fees")),
        }.WithBearerToken(login.AccessToken);
        var grantResponse = await client.SendAsync(grantRequest);
        Assert.Equal(HttpStatusCode.OK, grantResponse.StatusCode);
        var afterGrant = await grantResponse.Content.ReadFromJsonAsync<GuardianDto>();
        var activeGrant = Assert.Single(afterGrant!.ActiveAccessGrants);
        Assert.Equal("Fees", activeGrant.Category);

        // Category-scoped: granting Fees never implicitly grants Attendance/Grades.
        Assert.DoesNotContain(afterGrant.ActiveAccessGrants, g => g.Category is "Attendance" or "Grades");

        var revokeRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/student/students/me/guardians/{guardian.Id}/access-grants/Fees").WithBearerToken(login.AccessToken);
        var revokeResponse = await client.SendAsync(revokeRequest);
        Assert.Equal(HttpStatusCode.OK, revokeResponse.StatusCode);
        var afterRevoke = await revokeResponse.Content.ReadFromJsonAsync<GuardianDto>();
        Assert.Empty(afterRevoke!.ActiveAccessGrants);
    }

    [Fact]
    public async Task Linking_a_Guardian_without_any_contact_info_is_rejected()
    {
        var client = fixture.CreateClient();
        var (_, _, _, adminToken) = await StudentTestDataSeeder.ProvisionAdminAsync(fixture, client);
        var departmentId = await StudentTestDataSeeder.SeedDepartmentAsync(client, adminToken);
        var created = await StudentTestDataSeeder.CreateStudentRecordAsync(fixture, departmentId);
        var login = await StudentTestDataSeeder.ResetPasswordAndLoginAsync(fixture, client, created.IdentityUserId!.Value, "a-Kn0wn-Passw0rd!");

        var linkRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/student/students/me/guardians")
        {
            Content = JsonContent.Create(new LinkGuardianRequest("Karim Uddin", "Father", null, null)),
        }.WithBearerToken(login.AccessToken);
        var response = await client.SendAsync(linkRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_different_Students_own_account_cannot_see_or_manage_another_Students_Guardians()
    {
        var client = fixture.CreateClient();
        var (_, _, _, adminToken) = await StudentTestDataSeeder.ProvisionAdminAsync(fixture, client);
        var departmentId = await StudentTestDataSeeder.SeedDepartmentAsync(client, adminToken);

        var ownerCreated = await StudentTestDataSeeder.CreateStudentRecordAsync(fixture, departmentId);
        var ownerLogin = await StudentTestDataSeeder.ResetPasswordAndLoginAsync(fixture, client, ownerCreated.IdentityUserId!.Value, "a-Kn0wn-Passw0rd!");
        var linkRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/student/students/me/guardians")
        {
            Content = JsonContent.Create(new LinkGuardianRequest("Karim Uddin", "Father", "karim@example.edu.bd", null)),
        }.WithBearerToken(ownerLogin.AccessToken);
        var guardian = await (await client.SendAsync(linkRequest)).Content.ReadFromJsonAsync<GuardianDto>();

        var otherCreated = await StudentTestDataSeeder.CreateStudentRecordAsync(fixture, departmentId);
        var otherLogin = await StudentTestDataSeeder.ResetPasswordAndLoginAsync(fixture, client, otherCreated.IdentityUserId!.Value, "a-Different-Passw0rd!");

        // The other Student's own guardian list is empty - they never see the owner's Guardian.
        var otherListRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/student/students/me/guardians").WithBearerToken(otherLogin.AccessToken);
        var otherList = await (await client.SendAsync(otherListRequest)).Content.ReadFromJsonAsync<List<GuardianDto>>();
        Assert.Empty(otherList!);

        // Attempting to grant access against the owner's Guardian id, authenticated as the OTHER
        // Student, is rejected as a conflict - GuardianService.GrantAccessAsync resolves the
        // caller's OWN Student first, and that Guardian id doesn't exist under it.
        var crossGrantRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/student/students/me/guardians/{guardian!.Id}/access-grants")
        {
            Content = JsonContent.Create(new GrantGuardianAccessRequest("Fees")),
        }.WithBearerToken(otherLogin.AccessToken);
        var crossGrantResponse = await client.SendAsync(crossGrantRequest);
        Assert.Equal(HttpStatusCode.Conflict, crossGrantResponse.StatusCode);
    }
}

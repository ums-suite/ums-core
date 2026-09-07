using System.Net;
using System.Net.Http.Json;
using UMS.Modules.Student.Application.StudentRequests;
using UMS.Modules.Student.IntegrationTests.Infrastructure;

namespace UMS.Modules.Student.IntegrationTests.StudentRequests;

/// <summary>STU-9..STU-14 (requirement-spec.md student §2 Student-Initiated Requests, §4, §6).</summary>
[Collection(StudentApiTestCollectionDefinition.Name)]
public sealed class StudentRequestEndpointsTests(StudentApiFixture fixture)
{
    [Fact]
    public async Task Submitting_an_IdReissue_request_then_approving_it_reaches_Fulfilled()
    {
        var client = fixture.CreateClient();
        await StudentRequestTestDataSeeder.PublishDocumentTemplateAsync(fixture, client, "IdCard");
        var (_, _, _, adminToken) = await StudentTestDataSeeder.ProvisionAdminAsync(fixture, client);
        var departmentId = await StudentTestDataSeeder.SeedDepartmentAsync(client, adminToken);
        var created = await StudentTestDataSeeder.CreateStudentRecordAsync(fixture, departmentId);
        var login = await StudentTestDataSeeder.ResetPasswordAndLoginAsync(fixture, client, created.IdentityUserId!.Value, "a-Kn0wn-Passw0rd!");

        var submitResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/v1/student/students/requests")
        {
            Content = JsonContent.Create(new SubmitStudentRequestRequest("IdReissue", "Lost my card", null, null, false)),
        }.WithBearerToken(login.AccessToken));
        Assert.Equal(HttpStatusCode.Created, submitResponse.StatusCode);
        var submitted = await submitResponse.Content.ReadFromJsonAsync<StudentRequestDto>();
        Assert.Equal("Submitted", submitted!.Status);

        var (_, _, _, reviewerToken) = await StudentRequestTestDataSeeder.ProvisionReviewerAsync(fixture, client, scopeNodeId: null);
        var approveResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"/api/v1/student/students/requests/{submitted.Id}/approve")
        {
            Content = JsonContent.Create(new VersionedRequestBody(submitted.Version)),
        }.WithBearerToken(reviewerToken));

        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);
        var approved = await approveResponse.Content.ReadFromJsonAsync<StudentRequestDto>();
        Assert.Equal("Fulfilled", approved!.Status);
        Assert.NotNull(approved.GeneratedDocumentId);
    }

    [Fact]
    public async Task Submitting_an_IdReissue_request_for_a_Suspended_student_is_forbidden()
    {
        var client = fixture.CreateClient();
        var (_, _, _, adminToken) = await StudentTestDataSeeder.ProvisionAdminAsync(fixture, client);
        var departmentId = await StudentTestDataSeeder.SeedDepartmentAsync(client, adminToken);
        var created = await StudentTestDataSeeder.CreateStudentRecordAsync(fixture, departmentId);
        var login = await StudentTestDataSeeder.ResetPasswordAndLoginAsync(fixture, client, created.IdentityUserId!.Value, "a-Kn0wn-Passw0rd!");

        var initialVersion = await GetVersionAsync(client, login.AccessToken);
        var toActiveResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"/api/v1/student/students/{created.StudentId}/status")
        {
            Content = JsonContent.Create(new { status = "Active", reason = (string?)null, version = initialVersion }),
        }.WithBearerToken(adminToken));
        Assert.Equal(HttpStatusCode.OK, toActiveResponse.StatusCode);

        var activeVersion = await GetVersionAsync(client, login.AccessToken);
        var toSuspendedResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"/api/v1/student/students/{created.StudentId}/status")
        {
            Content = JsonContent.Create(new { status = "Suspended", reason = "disciplinary", version = activeVersion }),
        }.WithBearerToken(adminToken));
        Assert.Equal(HttpStatusCode.OK, toSuspendedResponse.StatusCode);

        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/v1/student/students/requests")
        {
            Content = JsonContent.Create(new SubmitStudentRequestRequest("IdReissue", "Lost my card", null, null, false)),
        }.WithBearerToken(login.AccessToken));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Submitting_a_second_open_request_of_the_same_type_is_a_conflict()
    {
        var client = fixture.CreateClient();
        var (_, _, _, adminToken) = await StudentTestDataSeeder.ProvisionAdminAsync(fixture, client);
        var departmentId = await StudentTestDataSeeder.SeedDepartmentAsync(client, adminToken);
        var created = await StudentTestDataSeeder.CreateStudentRecordAsync(fixture, departmentId);
        var login = await StudentTestDataSeeder.ResetPasswordAndLoginAsync(fixture, client, created.IdentityUserId!.Value, "a-Kn0wn-Passw0rd!");

        await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/v1/student/students/requests")
        {
            Content = JsonContent.Create(new SubmitStudentRequestRequest("TranscriptRequest", null, "Job application", null, false)),
        }.WithBearerToken(login.AccessToken));

        var second = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/v1/student/students/requests")
        {
            Content = JsonContent.Create(new SubmitStudentRequestRequest("TranscriptRequest", null, "Second application", null, false)),
        }.WithBearerToken(login.AccessToken));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task A_grievance_against_own_department_head_is_visible_only_to_a_Faculty_scoped_reviewer_not_a_Department_scoped_one()
    {
        var client = fixture.CreateClient();
        var (_, _, _, adminToken) = await StudentTestDataSeeder.ProvisionAdminAsync(fixture, client);
        var (departmentId, facultyId) = await StudentTestDataSeeder.SeedDepartmentWithFacultyAsync(client, adminToken);
        var created = await StudentTestDataSeeder.CreateStudentRecordAsync(fixture, departmentId);
        var login = await StudentTestDataSeeder.ResetPasswordAndLoginAsync(fixture, client, created.IdentityUserId!.Value, "a-Kn0wn-Passw0rd!");

        var submitResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/v1/student/students/requests")
        {
            Content = JsonContent.Create(new SubmitStudentRequestRequest("Grievance", null, null, "My Department Head retaliated against me", true)),
        }.WithBearerToken(login.AccessToken));
        Assert.Equal(HttpStatusCode.Created, submitResponse.StatusCode);
        var submitted = await submitResponse.Content.ReadFromJsonAsync<StudentRequestDto>();
        Assert.Equal(facultyId, submitted!.ReviewScopeNodeId);

        var (_, _, _, departmentReviewerToken) = await StudentRequestTestDataSeeder.ProvisionReviewerAsync(fixture, client, departmentId);
        var deptResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, $"/api/v1/student/students/requests/{submitted.Id}").WithBearerToken(departmentReviewerToken));
        Assert.Equal(HttpStatusCode.Forbidden, deptResponse.StatusCode);

        var (_, _, _, facultyReviewerToken) = await StudentRequestTestDataSeeder.ProvisionReviewerAsync(fixture, client, facultyId);
        var facultyResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, $"/api/v1/student/students/requests/{submitted.Id}").WithBearerToken(facultyReviewerToken));
        Assert.Equal(HttpStatusCode.OK, facultyResponse.StatusCode);
    }

    [Fact]
    public async Task Rejecting_a_request_without_a_reason_is_a_validation_error()
    {
        var client = fixture.CreateClient();
        var (_, _, _, adminToken) = await StudentTestDataSeeder.ProvisionAdminAsync(fixture, client);
        var departmentId = await StudentTestDataSeeder.SeedDepartmentAsync(client, adminToken);
        var created = await StudentTestDataSeeder.CreateStudentRecordAsync(fixture, departmentId);
        var login = await StudentTestDataSeeder.ResetPasswordAndLoginAsync(fixture, client, created.IdentityUserId!.Value, "a-Kn0wn-Passw0rd!");
        var submitResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/v1/student/students/requests")
        {
            Content = JsonContent.Create(new SubmitStudentRequestRequest("IdReissue", "Lost card", null, null, false)),
        }.WithBearerToken(login.AccessToken));
        var submitted = await submitResponse.Content.ReadFromJsonAsync<StudentRequestDto>();

        var (_, _, _, reviewerToken) = await StudentRequestTestDataSeeder.ProvisionReviewerAsync(fixture, client, scopeNodeId: null);
        var rejectResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"/api/v1/student/students/requests/{submitted!.Id}/reject")
        {
            Content = JsonContent.Create(new RejectStudentRequestRequest(string.Empty, submitted.Version)),
        }.WithBearerToken(reviewerToken));

        Assert.Equal(HttpStatusCode.BadRequest, rejectResponse.StatusCode);
    }

    private static async Task<uint> GetVersionAsync(HttpClient client, string accessToken)
    {
        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/api/v1/student/students/me").WithBearerToken(accessToken));
        var dto = await response.Content.ReadFromJsonAsync<UMS.Modules.Student.Application.Students.StudentDto>();
        return dto!.Version;
    }
}

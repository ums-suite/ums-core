using System.Net;
using System.Net.Http.Json;
using UMS.Modules.Student.Application.Students;
using UMS.Modules.Student.IntegrationTests.Infrastructure;

namespace UMS.Modules.Student.IntegrationTests.Students;

/// <summary>
/// STU-8 (requirement-spec.md student §6 <c>POST /students/{id}/status</c>; edge-cases.md, "Two
/// admins issue conflicting status changes to the same Student concurrently").
/// </summary>
[Collection(StudentApiTestCollectionDefinition.Name)]
public sealed class StudentStatusEndpointsTests(StudentApiFixture fixture)
{
    private static readonly string[] SuspendedOrTransferred = ["Suspended", "Transferred"];

    [Fact]
    public async Task Full_lifecycle_Enrolled_to_Active_to_Suspended_to_Active_reinstatement_to_Graduated()
    {
        var client = fixture.CreateClient();
        var (_, _, _, adminToken) = await StudentTestDataSeeder.ProvisionAdminAsync(fixture, client);
        var departmentId = await StudentTestDataSeeder.SeedDepartmentAsync(client, adminToken);
        var created = await StudentTestDataSeeder.CreateStudentRecordAsync(fixture, departmentId);
        var initialVersion = await GetVersionAsync(client, adminToken, created.StudentId);

        var active = await ChangeStatusAsync(client, adminToken, created.StudentId, "Active", null, initialVersion);
        Assert.Equal(HttpStatusCode.OK, active.StatusCode);
        var activeDto = await active.Content.ReadFromJsonAsync<StudentDto>();
        Assert.Equal("Active", activeDto!.Status);

        var suspended = await ChangeStatusAsync(client, adminToken, created.StudentId, "Suspended", "Disciplinary action", activeDto.Version);
        var suspendedDto = await suspended.Content.ReadFromJsonAsync<StudentDto>();
        Assert.Equal("Suspended", suspendedDto!.Status);

        var reinstated = await ChangeStatusAsync(client, adminToken, created.StudentId, "Active", "Reinstated after review", suspendedDto.Version);
        var reinstatedDto = await reinstated.Content.ReadFromJsonAsync<StudentDto>();
        Assert.Equal("Active", reinstatedDto!.Status);

        var graduated = await ChangeStatusAsync(client, adminToken, created.StudentId, "Graduated", "Completed degree requirements", reinstatedDto.Version);
        var graduatedDto = await graduated.Content.ReadFromJsonAsync<StudentDto>();
        Assert.Equal("Graduated", graduatedDto!.Status);
    }

    [Fact]
    public async Task Enrolled_to_Graduated_directly_is_rejected_as_an_illegal_transition()
    {
        var client = fixture.CreateClient();
        var (_, _, _, adminToken) = await StudentTestDataSeeder.ProvisionAdminAsync(fixture, client);
        var departmentId = await StudentTestDataSeeder.SeedDepartmentAsync(client, adminToken);
        var created = await StudentTestDataSeeder.CreateStudentRecordAsync(fixture, departmentId);
        var initialVersion = await GetVersionAsync(client, adminToken, created.StudentId);

        var response = await ChangeStatusAsync(client, adminToken, created.StudentId, "Graduated", null, initialVersion);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Status_change_requires_the_StatusChange_permission()
    {
        var client = fixture.CreateClient();
        var (_, _, _, adminToken) = await StudentTestDataSeeder.ProvisionAdminAsync(fixture, client);
        var departmentId = await StudentTestDataSeeder.SeedDepartmentAsync(client, adminToken);
        var created = await StudentTestDataSeeder.CreateStudentRecordAsync(fixture, departmentId);

        var plainUser = await TestUsers.ProvisionAsync(client);
        var plainLogin = await TestUsers.LoginAsync(client, plainUser.Username);

        var response = await ChangeStatusAsync(client, plainLogin.AccessToken, created.StudentId, "Active", null, 0);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// edge-cases.md "Two admins issue conflicting status changes to the same Student
    /// concurrently": the loser is rejected outright (never silently overwritten) and shown the
    /// CURRENT state so it can re-decide, not blind-retry its own originally-intended transition.
    /// </summary>
    [Fact]
    public async Task Concurrent_conflicting_status_changes_the_loser_is_rejected_and_shown_current_state()
    {
        var client = fixture.CreateClient();
        var (_, _, _, adminToken) = await StudentTestDataSeeder.ProvisionAdminAsync(fixture, client);
        var departmentId = await StudentTestDataSeeder.SeedDepartmentAsync(client, adminToken);
        var created = await StudentTestDataSeeder.CreateStudentRecordAsync(fixture, departmentId);
        var initialVersion = await GetVersionAsync(client, adminToken, created.StudentId);

        var activeResponse = await ChangeStatusAsync(client, adminToken, created.StudentId, "Active", null, initialVersion);
        var activeDto = await activeResponse.Content.ReadFromJsonAsync<StudentDto>();

        // Both admins read the SAME version (activeDto.Version) before either commits - a genuine
        // race, not a sequential retry.
        var suspendTask = ChangeStatusAsync(client, adminToken, created.StudentId, "Suspended", "Admin A: disciplinary", activeDto!.Version);
        var transferTask = ChangeStatusAsync(client, adminToken, created.StudentId, "Transferred", "Admin B: transferred out", activeDto.Version);
        var results = await Task.WhenAll(suspendTask, transferTask);

        var successCount = results.Count(r => r.StatusCode == HttpStatusCode.OK);
        var conflictCount = results.Count(r => r.StatusCode == HttpStatusCode.Conflict);

        Assert.Equal(1, successCount);
        Assert.Equal(1, conflictCount);

        var conflictResponse = results.First(r => r.StatusCode == HttpStatusCode.Conflict);
        var conflictBody = await conflictResponse.Content.ReadFromJsonAsync<StudentStatusConflict>();
        Assert.NotNull(conflictBody);
        // The current state shown to the loser must be one of the two legal outcomes - never a
        // third, corrupted state - and must NOT still read "Active" (a real transition committed).
        Assert.Contains(conflictBody!.CurrentState.Status, SuspendedOrTransferred);

        // A follow-up GET confirms exactly one of the two transitions actually took effect.
        var getRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/student/students/{created.StudentId}").WithBearerToken(adminToken);
        var finalState = await (await client.SendAsync(getRequest)).Content.ReadFromJsonAsync<StudentDto>();
        Assert.Contains(finalState!.Status, SuspendedOrTransferred);

        // edge-cases.md's own residual note: BOTH admins' attempts are independently audited, even
        // though only one was ever applied.
        await using var connection = new Npgsql.NpgsqlConnection(fixture.PostgresConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT action FROM audit.audit_log_entries WHERE entity_type = 'Student' AND entity_id = @id AND action IN ('status_change', 'status_change_rejected') ORDER BY occurred_at DESC LIMIT 2";
        command.Parameters.AddWithValue("id", created.StudentId.ToString());
        await using var reader = await command.ExecuteReaderAsync();
        var actions = new List<string>();
        while (await reader.ReadAsync())
        {
            actions.Add(reader.GetString(0));
        }

        Assert.Contains("status_change", actions);
        Assert.Contains("status_change_rejected", actions);
    }

    private static async Task<uint> GetVersionAsync(HttpClient client, string accessToken, Guid studentId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/student/students/{studentId}").WithBearerToken(accessToken);
        var response = await client.SendAsync(request);
        var dto = await response.Content.ReadFromJsonAsync<StudentDto>();
        return dto!.Version;
    }

    private static async Task<HttpResponseMessage> ChangeStatusAsync(HttpClient client, string accessToken, Guid studentId, string status, string? reason, uint version)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/student/students/{studentId}/status")
        {
            Content = JsonContent.Create(new ChangeStudentStatusRequest(status, reason, version)),
        }.WithBearerToken(accessToken);
        return await client.SendAsync(request);
    }
}

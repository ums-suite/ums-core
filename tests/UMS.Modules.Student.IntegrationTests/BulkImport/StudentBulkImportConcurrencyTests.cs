using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Student.Application.BulkImport;
using UMS.Modules.Student.Application.Students;
using UMS.Modules.Student.IntegrationTests.Infrastructure;

namespace UMS.Modules.Student.IntegrationTests.BulkImport;

/// <summary>
/// STU-15's own concurrency-sensitive invariant (ums-conventions.md, Testing) - edge-cases.md's "A
/// bulk-import row upsert races a student's own concurrent self-service profile edit," resolved by
/// design-decisions.md's "Bulk-Import Concurrency &amp; Field-Scoping Design" (field-scoped writes plus
/// optimistic concurrency on any overlapping field). Fires a genuinely concurrent
/// <c>PUT /students/me</c> HTTP call against an in-process
/// <see cref="StudentBulkImportProcessingService.ProcessNextBatchAsync"/> call, both racing against
/// the SAME captured baseline <c>Student.Version</c> - real Postgres, real <c>xmin</c> check, exactly
/// like <c>CreateStudentRecordConcurrencyTests</c>' own real-concurrency shape.
/// </summary>
[Collection(StudentApiTestCollectionDefinition.Name)]
public sealed class StudentBulkImportConcurrencyTests(StudentApiFixture fixture)
{
    [Fact]
    public async Task A_bulk_import_update_row_racing_a_concurrent_self_service_edit_never_silently_overwrites_the_loser()
    {
        var client = fixture.CreateClient();
        var (_, _, _, adminToken) = await StudentTestDataSeeder.ProvisionAdminAsync(fixture, client);
        var departmentId = await StudentTestDataSeeder.SeedDepartmentAsync(client, adminToken);
        var (_, _, _, bulkImportToken) = await StudentRequestTestDataSeeder.ProvisionBulkImportAdminAsync(fixture, client);

        var created = await StudentTestDataSeeder.CreateStudentRecordAsync(fixture, departmentId);
        var login = await StudentTestDataSeeder.ResetPasswordAndLoginAsync(fixture, client, created.IdentityUserId!.Value, "a-Kn0wn-Passw0rd!");

        var beforeResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/api/v1/student/students/me").WithBearerToken(login.AccessToken));
        var before = await beforeResponse.Content.ReadFromJsonAsync<StudentDto>();
        var baselineVersion = before!.Version;

        var updateRow = new StudentBulkImportRowInput(null, created.StudentNumber, baselineVersion, null, null, null, null, null, null, null, null, null, null, null, null, "from-bulk-import@example.edu.bd", null, null);
        var uploadResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/v1/student/students/bulk-import")
        {
            Content = JsonContent.Create(new UploadStudentBulkImportRequest([updateRow])),
        }.WithBearerToken(bulkImportToken));
        var uploaded = await uploadResponse.Content.ReadFromJsonAsync<StudentBulkImportJobDto>();
        await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"/api/v1/student/students/bulk-import/{uploaded!.Id}/approve").WithBearerToken(bulkImportToken));

        // Genuinely concurrent: the self-service PUT (a real HTTP request, its own DbContext scope)
        // races the bulk-import row's own field-scoped write (a direct call into a FRESH DI scope's
        // StudentBulkImportProcessingService, mirroring the real worker) - both starting from the
        // IDENTICAL baseline version captured above.
        var selfServiceTask = client.SendAsync(new HttpRequestMessage(HttpMethod.Put, "/api/v1/student/students/me")
        {
            Content = JsonContent.Create(new UpdateSelfServiceProfileRequest(null, "+8801912345678", null, baselineVersion)),
        }.WithBearerToken(login.AccessToken));

        var bulkImportTask = Task.Run(async () =>
        {
            using var scope = fixture.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<StudentBulkImportProcessingService>().ProcessNextBatchAsync(uploaded.Id, 10);
        });

        await Task.WhenAll(selfServiceTask, bulkImportTask);

        var selfServiceResponse = await selfServiceTask;
        var selfServiceWon = selfServiceResponse.StatusCode == HttpStatusCode.OK;

        var reportResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, $"/api/v1/student/students/bulk-import/{uploaded.Id}").WithBearerToken(bulkImportToken));
        var report = await reportResponse.Content.ReadFromJsonAsync<StudentBulkImportJobReportDto>();
        var bulkImportRow = report!.Rows.Single();
        var bulkImportWon = bulkImportRow.Status == "Succeeded";

        // Exactly one writer wins the race - the DB-level xmin check makes both winning (or both
        // losing) structurally impossible; the loser is REJECTED, never silently overwritten.
        Assert.NotEqual(selfServiceWon, bulkImportWon);
        if (!selfServiceWon)
        {
            Assert.Equal(HttpStatusCode.Conflict, selfServiceResponse.StatusCode);
        }

        if (!bulkImportWon)
        {
            Assert.Equal("Failed", bulkImportRow.Status);
            Assert.Contains("version", bulkImportRow.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        var finalResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/api/v1/student/students/me").WithBearerToken(login.AccessToken));
        var final = await finalResponse.Content.ReadFromJsonAsync<StudentDto>();
        if (selfServiceWon)
        {
            Assert.Equal("+8801912345678", final!.ContactPhone);
        }
        else
        {
            Assert.Equal("from-bulk-import@example.edu.bd", final!.ContactEmail);
        }
    }
}

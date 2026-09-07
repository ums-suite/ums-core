using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Student.Application.Abstractions;
using UMS.Modules.Student.Application.BulkImport;
using UMS.Modules.Student.Domain.BulkImport;
using UMS.Modules.Student.IntegrationTests.Infrastructure;

namespace UMS.Modules.Student.IntegrationTests.BulkImport;

/// <summary>
/// STU-15/STU-16 end to end against real Postgres (requirement-spec.md student §2 Bulk Import, §6).
/// <c>UMS.Workers</c>' <c>StudentBulkImportRelayWorker</c> itself is not running in this
/// WebApplicationFactory-hosted test process - <see cref="DrainJobAsync(IServiceProvider, Guid)"/>
/// calls the exact same public <see cref="StudentBulkImportProcessingService"/> the real worker
/// calls, against the same DI container, so the full row-processing path (real Postgres reads/
/// writes, real <c>CreateStudentRecordService</c> reuse) is exercised for real - mirrors Documents'
/// own <c>BulkGenerationEndpointsTests.DrainJobAsync</c> precedent exactly.
/// </summary>
[Collection(StudentApiTestCollectionDefinition.Name)]
public sealed class StudentBulkImportEndpointsTests(StudentApiFixture fixture)
{
    [Fact]
    public async Task Upload_Approve_Process_report_round_trip_with_a_mix_of_valid_and_invalid_rows()
    {
        var client = fixture.CreateClient();
        var (_, _, _, adminToken) = await StudentTestDataSeeder.ProvisionAdminAsync(fixture, client);
        var departmentId = await StudentTestDataSeeder.SeedDepartmentAsync(client, adminToken);
        var (_, _, _, bulkImportToken) = await StudentRequestTestDataSeeder.ProvisionBulkImportAdminAsync(fixture, client);

        var validRow = new StudentBulkImportRowInput(Guid.NewGuid(), null, null, 2026, "CSE", departmentId, Guid.NewGuid(), "Rahim", "Uddin", null, null, $"bulk-{Guid.NewGuid():N}@example.edu.bd", null, new DateOnly(2005, 1, 1), null, null, null, null);
        var invalidRow = new StudentBulkImportRowInput(null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null);

        var uploadResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/v1/student/students/bulk-import")
        {
            Content = JsonContent.Create(new UploadStudentBulkImportRequest([validRow, invalidRow])),
        }.WithBearerToken(bulkImportToken));
        Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);
        var uploaded = await uploadResponse.Content.ReadFromJsonAsync<StudentBulkImportJobDto>();
        Assert.Equal("Validated", uploaded!.Status);
        Assert.Equal(1, uploaded.ValidRowCount);
        Assert.Equal(1, uploaded.InvalidRowCount);

        var previewResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, $"/api/v1/student/students/bulk-import/{uploaded.Id}").WithBearerToken(bulkImportToken));
        var preview = await previewResponse.Content.ReadFromJsonAsync<StudentBulkImportJobReportDto>();
        Assert.Contains(preview!.Rows, r => r.Status == "Invalid" && r.ErrorMessage is not null);

        var approveResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"/api/v1/student/students/bulk-import/{uploaded.Id}/approve").WithBearerToken(bulkImportToken));
        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);

        using (var scope = fixture.Services.CreateScope())
        {
            await DrainJobAsync(scope.ServiceProvider, uploaded.Id);
        }

        var finalResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, $"/api/v1/student/students/bulk-import/{uploaded.Id}").WithBearerToken(bulkImportToken));
        var final = await finalResponse.Content.ReadFromJsonAsync<StudentBulkImportJobReportDto>();
        // The Invalid row never reaches processing at all (it never counts toward FailedCount) -
        // with the one Valid row succeeding, the job reaches plain Completed, not
        // CompletedWithErrors (that status is reserved for a row that was attempted and failed).
        Assert.Equal("Completed", final!.Job.Status);
        Assert.Equal(1, final.Job.SucceededCount);
        Assert.Equal(0, final.Job.FailedCount);
        Assert.Contains(final.Rows, r => r.Status == "Succeeded" && r.ResultStudentId is not null);
        Assert.Contains(final.Rows, r => r.Status == "Invalid");
    }

    [Fact]
    public async Task Bulk_import_endpoints_require_the_BulkImportExecute_permission()
    {
        var client = fixture.CreateClient();
        var plainUser = await TestUsers.ProvisionAsync(client);
        var plainLogin = await TestUsers.LoginAsync(client, plainUser.Username);

        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/v1/student/students/bulk-import")
        {
            Content = JsonContent.Create(new UploadStudentBulkImportRequest([])),
        }.WithBearerToken(plainLogin.AccessToken));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Approving_a_job_with_only_invalid_rows_fails()
    {
        var client = fixture.CreateClient();
        var (_, _, _, bulkImportToken) = await StudentRequestTestDataSeeder.ProvisionBulkImportAdminAsync(fixture, client);
        var invalidRow = new StudentBulkImportRowInput(null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null);

        var uploadResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/v1/student/students/bulk-import")
        {
            Content = JsonContent.Create(new UploadStudentBulkImportRequest([invalidRow])),
        }.WithBearerToken(bulkImportToken));
        var uploaded = await uploadResponse.Content.ReadFromJsonAsync<StudentBulkImportJobDto>();

        var approveResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"/api/v1/student/students/bulk-import/{uploaded!.Id}/approve").WithBearerToken(bulkImportToken));

        Assert.Equal(HttpStatusCode.BadRequest, approveResponse.StatusCode);
    }

    internal static async Task DrainJobAsync(IServiceProvider services, Guid jobId)
    {
        var processing = services.GetRequiredService<StudentBulkImportProcessingService>();
        var jobs = services.GetRequiredService<IStudentBulkImportJobRepository>();

        for (var i = 0; i < 20; i++)
        {
            await processing.ProcessNextBatchAsync(jobId, 50);
            var job = await jobs.GetByIdAsync(new StudentBulkImportJobId(jobId));
            if (job is { Status: StudentBulkImportJobStatus.Completed or StudentBulkImportJobStatus.CompletedWithErrors })
            {
                return;
            }
        }

        throw new InvalidOperationException($"StudentBulkImportJob '{jobId}' did not reach a terminal status within the drain loop's bounded attempts.");
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Audit.Application.Abstractions;
using UMS.Modules.Audit.Application.Entries;
using UMS.Modules.Audit.Application.Exports;
using UMS.Modules.Audit.Domain.Exports;
using UMS.Modules.Audit.IntegrationTests.Infrastructure;
using UMS.Shared.Audit;

namespace UMS.Modules.Audit.IntegrationTests.Exports;

/// <summary>
/// AUD-9/AUD-10: request creation and status/download through the real HTTP API, plus the async
/// processing half - normally <c>UMS.Workers</c>' <c>AuditExportRelayWorker</c>, which this
/// WebApplicationFactory-hosted test process does not itself run (it boots <c>UMS.Host</c>, not
/// <c>UMS.Workers</c>). <see cref="ProcessOnePendingExportAsync"/> replicates that worker's small
/// processing step directly against the same DI container and the same MinIO Testcontainer
/// (see AuditApiFixture) so the export's full path - Postgres query, CSV render, real S3-API
/// upload, status update - is exercised for real, not mocked.
/// </summary>
[Collection(AuditApiTestCollectionDefinition.Name)]
public class AuditExportTests(AuditApiFixture fixture)
{
    [Fact]
    public async Task Requesting_an_export_creates_a_pending_job_and_status_is_queryable()
    {
        using var client = fixture.CreateClient();
        var (_, adminUsername, adminPassword, _) = await TestDataSeeder.ProvisionAdminAsync(fixture, client);
        var accessToken = await TestAuth.LoginAsync(client, adminUsername, adminPassword);

        var postRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/audit/exports")
        {
            Content = JsonContent.Create(new { entityType = "Grade", format = "Csv" }),
        }.WithBearerToken(accessToken);
        var postResponse = await client.SendAsync(postRequest);
        Assert.Equal(HttpStatusCode.Created, postResponse.StatusCode);

        var created = await postResponse.Content.ReadFromJsonAsync<AuditExportRequestDto>();
        Assert.Equal("Pending", created!.Status);

        var getRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/audit/exports/{created.Id}").WithBearerToken(accessToken);
        var getResponse = await client.SendAsync(getRequest);
        getResponse.EnsureSuccessStatusCode();
        var fetched = await getResponse.Content.ReadFromJsonAsync<AuditExportRequestDto>();

        Assert.Equal("Pending", fetched!.Status);
        Assert.Null(fetched.DownloadUrl);
    }

    [Fact]
    public async Task GetStatus_for_an_unknown_export_id_returns_not_found()
    {
        using var client = fixture.CreateClient();
        var (_, adminUsername, adminPassword, _) = await TestDataSeeder.ProvisionAdminAsync(fixture, client);
        var accessToken = await TestAuth.LoginAsync(client, adminUsername, adminPassword);

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/audit/exports/{Guid.NewGuid()}").WithBearerToken(accessToken);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task An_end_to_end_export_completes_with_a_downloadable_csv_matching_the_filter()
    {
        using var client = fixture.CreateClient();
        var (_, adminUsername, adminPassword, _) = await TestDataSeeder.ProvisionAdminAsync(fixture, client);
        var accessToken = await TestAuth.LoginAsync(client, adminUsername, adminPassword);

        var entityId = $"export-{Guid.NewGuid():N}";
        var correlationId = $"export-corr-{Guid.NewGuid():N}";
        await WriteEntryAsync("Payment", entityId, AuditActions.Create, correlationId);

        var postRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/audit/exports")
        {
            Content = JsonContent.Create(new { entityType = "Payment", entityId, format = "Csv" }),
        }.WithBearerToken(accessToken);
        var postResponse = await client.SendAsync(postRequest);
        postResponse.EnsureSuccessStatusCode();
        var created = await postResponse.Content.ReadFromJsonAsync<AuditExportRequestDto>();

        await ProcessPendingExportAsync(created!.Id);

        var getRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/audit/exports/{created!.Id}").WithBearerToken(accessToken);
        var getResponse = await client.SendAsync(getRequest);
        getResponse.EnsureSuccessStatusCode();
        var completed = await getResponse.Content.ReadFromJsonAsync<AuditExportRequestDto>();

        Assert.Equal("Completed", completed!.Status);
        Assert.NotNull(completed.DownloadUrl);

        using var downloadClient = new HttpClient();
        var csv = await downloadClient.GetStringAsync(completed.DownloadUrl);
        Assert.Contains(entityId, csv);
    }

    private async Task WriteEntryAsync(string entityType, string entityId, string action, string correlationId)
    {
        using var scope = fixture.Services.CreateScope();
        var identityUnitOfWork = scope.ServiceProvider.GetRequiredService<UMS.Modules.Identity.Application.Abstractions.IUnitOfWork>();
        var auditRecorder = scope.ServiceProvider.GetRequiredService<IAuditRecorder>();

        var request = new RecordAuditEntryRequest(
            ActorId: "system:test-writer",
            ActorType: AuditActorType.System,
            IpAddress: null,
            Application: "system",
            EntityType: entityType,
            EntityId: entityId,
            Action: action,
            BeforeValueJson: null,
            AfterValueJson: "{}",
            CorrelationId: correlationId);

        await using var transaction = await identityUnitOfWork.BeginTransactionAsync();
        var result = await auditRecorder.RecordEntryAsync(request, transaction.DbTransaction);
        Assert.True(result.IsSuccess);
        await transaction.CommitAsync();
    }

    /// <summary>
    /// A minimal, direct replica of AuditExportRelayWorker's CSV processing branch - see this
    /// class's own remarks. Selects the outbox message matching <paramref name="exportRequestId"/>
    /// rather than assuming there is exactly one pending message, since other tests in this same
    /// (sequentially-run) collection may have their own still-unprocessed export requests.
    /// </summary>
    private async Task ProcessPendingExportAsync(Guid exportRequestId)
    {
        using var scope = fixture.Services.CreateScope();
        var outbox = scope.ServiceProvider.GetRequiredService<IOutboxReader>();
        var exportRequests = scope.ServiceProvider.GetRequiredService<IAuditExportRequestRepository>();
        var entries = scope.ServiceProvider.GetRequiredService<IAuditLogEntryRepository>();
        var objectStorage = scope.ServiceProvider.GetRequiredService<IObjectStorage>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var messages = await outbox.GetUnprocessedAsync("AuditExportRequested", batchSize: 50);
        var message = messages.Single(m => JsonSerializer.Deserialize<AuditExportRequestedPayload>(m.PayloadJson)!.ExportRequestId == exportRequestId);

        var request = await exportRequests.GetByIdAsync(exportRequestId);
        Assert.NotNull(request);

        request!.MarkProcessing();
        await unitOfWork.SaveChangesAsync();

        var filter = JsonSerializer.Deserialize<AuditEntryFilter>(request.FilterJson) ?? new AuditEntryFilter();
        var matched = await entries.ListAllAsync(filter);
        var csv = AuditEntryCsvRenderer.Render(matched.Select(AuditLogEntryDto.FromDomain));

        var objectKey = $"audit-exports/{request.Id}.csv";
        await using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv)))
        {
            await objectStorage.UploadAsync(objectKey, stream, "text/csv");
        }

        request.MarkCompleted(objectKey, DateTimeOffset.UtcNow);
        await unitOfWork.SaveChangesAsync();
        await outbox.MarkProcessedAsync(message.Id);
    }
}

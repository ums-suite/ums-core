using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Documents.Api.Contracts;
using UMS.Modules.Documents.Application.Abstractions;
using UMS.Modules.Documents.Application.BulkJobs;
using UMS.Modules.Documents.Application.Generation;
using UMS.Modules.Documents.Application.Templates;
using UMS.Modules.Documents.Domain.GeneratedDocuments;
using UMS.Modules.Documents.IntegrationTests.Infrastructure;

namespace UMS.Modules.Documents.IntegrationTests.BulkJobs;

/// <summary>
/// DOC-4/DOC-5/DOC-6/DOC-7: the bulk/async path end to end against real Postgres + MinIO.
/// <c>UMS.Workers</c>' <c>BulkGenerationRelayWorker</c> itself is not running in this
/// WebApplicationFactory-hosted test process (it boots <c>UMS.Host</c>) - <see cref="DrainJobAsync"/>
/// replicates that worker's per-item loop directly against the same DI container, using the same
/// public <see cref="GeneratedDocumentPipeline"/> the real worker calls, so the full path (Postgres
/// item tracking, real MinIO upload, checksum verification) is exercised for real - mirrors
/// Audit's own <c>AuditExportTests.ProcessPendingExportAsync</c> precedent.
/// </summary>
[Collection(DocumentsApiTestCollectionDefinition.Name)]
public class BulkGenerationEndpointsTests(DocumentsApiFixture fixture)
{
    [Fact]
    public async Task A_bulk_job_processes_every_item_and_reaches_Completed()
    {
        using var client = fixture.CreateClient();
        var (_, username, password, _) = await TestDataSeeder.ProvisionAdminAsync(fixture, client);
        var accessToken = await TestAuth.LoginAsync(client, username, password);
        await PublishTemplateAsync(client, accessToken, "Transcript");

        var items = Enumerable.Range(0, 5)
            .Select(i => new BulkGenerationItemRequestBody(Guid.NewGuid(), Guid.NewGuid(), new Dictionary<string, string> { ["seat"] = $"A{i}" }))
            .ToList();
        var requestBody = new RequestBulkGenerationRequestBody("Transcript", items);

        var postResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/v1/documents/generate-bulk") { Content = JsonContent.Create(requestBody) }.WithBearerToken(accessToken));
        Assert.Equal(HttpStatusCode.Accepted, postResponse.StatusCode);
        var created = await postResponse.Content.ReadFromJsonAsync<BulkGenerationJobDto>();
        Assert.Equal(5, created!.TotalItems);

        await DrainJobAsync(created.Id);

        var statusResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, $"/api/v1/documents/jobs/{created.Id}").WithBearerToken(accessToken));
        statusResponse.EnsureSuccessStatusCode();
        var status = await statusResponse.Content.ReadFromJsonAsync<BulkGenerationJobDto>();

        Assert.Equal("Completed", status!.Status);
        Assert.Equal(5, status.CompletedCount);
        Assert.Equal(0, status.DeadLetteredCount);
    }

    [Fact]
    public async Task Job_status_for_an_unknown_id_returns_not_found()
    {
        using var client = fixture.CreateClient();
        var (_, username, password, _) = await TestDataSeeder.ProvisionAdminAsync(fixture, client);
        var accessToken = await TestAuth.LoginAsync(client, username, password);

        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, $"/api/v1/documents/jobs/{Guid.NewGuid()}").WithBearerToken(accessToken));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task PublishTemplateAsync(HttpClient client, string accessToken, string documentType)
    {
        var body = new PublishTemplateRequestBody(documentType, null, [new TemplateTranslationRequestBody("en", documentType, "{}")]);
        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/v1/documents/templates") { Content = JsonContent.Create(body) }.WithBearerToken(accessToken));
        response.EnsureSuccessStatusCode();
    }

    private async Task DrainJobAsync(Guid jobId)
    {
        using var scope = fixture.Services.CreateScope();
        var jobs = scope.ServiceProvider.GetRequiredService<IBulkGenerationJobRepository>();
        var jobItems = scope.ServiceProvider.GetRequiredService<IBulkGenerationJobItemRepository>();
        var templates = scope.ServiceProvider.GetRequiredService<IDocumentTemplateRepository>();
        var documents = scope.ServiceProvider.GetRequiredService<IGeneratedDocumentRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var pipeline = scope.ServiceProvider.GetRequiredService<GeneratedDocumentPipeline>();
        var verificationIdGenerator = scope.ServiceProvider.GetRequiredService<IVerificationIdGenerator>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var job = await jobs.GetByIdAsync(new UMS.Modules.Documents.Domain.BulkJobs.BulkGenerationJobId(jobId));
        Assert.NotNull(job);

        job!.MarkProcessing();
        await unitOfWork.SaveChangesAsync();

        var template = await templates.GetByIdAsync(job.TemplateId) ?? throw new InvalidOperationException("Pinned template missing.");

        UMS.Modules.Documents.Domain.BulkJobs.BulkGenerationJobItem[] batch;
        while ((batch = (await jobItems.GetUnresolvedBatchAsync(job.Id, 100)).ToArray()).Length > 0)
        {
            foreach (var item in batch)
            {
                var claim = GeneratedDocument.Claim(
                    item.OwnerId,
                    job.DocumentType,
                    item.SourceReferenceId,
                    template.Id,
                    template.Version,
                    verificationIdGenerator.NewId(),
                    item.RenderDataJson,
                    UMS.Modules.Documents.Domain.Common.LanguageCodeExtensions.Fallback,
                    clock.UtcNow,
                    bulkGenerationJobId: job.Id.Value);

                await documents.AddAsync(claim);

                item.MarkProcessing();
                await unitOfWork.SaveChangesAsync();

                var outcome = await pipeline.RunAsync(claim, template, "test-drain", CancellationToken.None);
                Assert.Equal(PipelineOutcome.Ready, outcome);

                item.MarkCompleted(claim.Id);
                job.RecordItemOutcome(deadLettered: false);
                await unitOfWork.SaveChangesAsync();
            }
        }

        var completed = job.Complete(clock.UtcNow);
        Assert.True(completed.IsSuccess);
        await unitOfWork.SaveChangesAsync();
    }
}

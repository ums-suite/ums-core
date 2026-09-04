using UMS.Modules.Documents.Application.BulkJobs;
using UMS.Modules.Documents.Domain.Common;
using UMS.Modules.Documents.Domain.Templates;
using UMS.Modules.Documents.UnitTests.TestDoubles;

namespace UMS.Modules.Documents.UnitTests.BulkJobs;

/// <summary>design-decisions.md's BulkGenerationJob template-version-pinning decision: "a template edit mid-batch never changes documents already queued or rendering in that batch."</summary>
public sealed class RequestBulkGenerationServiceTests
{
    [Fact]
    public async Task RequestAsync_pins_the_current_template_version_at_job_creation_time()
    {
        var templates = new FakeDocumentTemplateRepository();
        templates.Add(Publish(version: 1));
        templates.Add(Publish(version: 2));

        var jobs = new FakeBulkGenerationJobRepository();
        var jobItems = new FakeBulkGenerationJobItemRepository();
        var service = new RequestBulkGenerationService(jobs, jobItems, templates, new FakeUnitOfWork(), new FakeOutboxEnqueuer(), new FakeClock());

        var command = new RequestBulkGenerationCommand(
            DocumentType.AdmitCard,
            Guid.NewGuid(),
            [new RequestBulkGenerationItemInput(Guid.NewGuid(), Guid.NewGuid(), new Dictionary<string, string>())]);

        var result = await service.RequestAsync(command);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.TemplateVersion);

        // A publish landing AFTER job creation must never retroactively change the pinned version.
        templates.Add(Publish(version: 3));
        var jobAfterLaterPublish = await jobs.GetByIdAsync(new Domain.BulkJobs.BulkGenerationJobId(result.Value.Id));
        Assert.Equal(2, jobAfterLaterPublish!.TemplateVersion);
    }

    [Fact]
    public async Task RequestAsync_with_no_items_is_rejected()
    {
        var templates = new FakeDocumentTemplateRepository();
        templates.Add(Publish(version: 1));
        var service = new RequestBulkGenerationService(new FakeBulkGenerationJobRepository(), new FakeBulkGenerationJobItemRepository(), templates, new FakeUnitOfWork(), new FakeOutboxEnqueuer(), new FakeClock());

        var command = new RequestBulkGenerationCommand(DocumentType.AdmitCard, Guid.NewGuid(), []);

        var result = await service.RequestAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal("bulk_generation_job.items_required", result.Error!.Code);
    }

    private static DocumentTemplate Publish(int version)
    {
        var translations = new Dictionary<LanguageCode, (string Title, string LabelsJson)>
        {
            [LanguageCode.En] = ("Admit Card", "{}"),
        };
        return DocumentTemplate.Create(DocumentType.AdmitCard, version, null, translations, DateTimeOffset.UtcNow).Value;
    }
}

using Microsoft.Extensions.Logging.Abstractions;
using UMS.Modules.Documents.Application.Generation;
using UMS.Modules.Documents.Domain.Common;
using UMS.Modules.Documents.UnitTests.TestDoubles;

namespace UMS.Modules.Documents.UnitTests.Generation;

/// <summary>
/// DOC-3/edge-cases.md: exercises the idempotent-generation invariant end to end through the
/// Application layer (with in-memory fakes, not a real Postgres unique constraint - see
/// <see cref="InMemoryGeneratedDocumentRepository"/>'s own remarks on scope). True concurrent-request
/// timing against a real database is covered separately by the integration-test suite.
/// </summary>
public sealed class GenerateDocumentServiceTests
{
    private static (GenerateDocumentService Service, InMemoryGeneratedDocumentRepository Documents, FakeDocumentTemplateRepository Templates) BuildService()
    {
        var documents = new InMemoryGeneratedDocumentRepository();
        var templates = new FakeDocumentTemplateRepository();
        var unitOfWork = new FakeUnitOfWork();
        var objectStorage = new FakeObjectStorage();
        var outbox = new FakeOutboxEnqueuer();
        var auditRecorder = new FakeAuditRecorder();
        var clock = new FakeClock();

        var pipeline = new GeneratedDocumentPipeline(
            new FakeDocumentRenderer(),
            objectStorage,
            unitOfWork,
            outbox,
            auditRecorder,
            clock,
            NullLogger<GeneratedDocumentPipeline>.Instance);

        var service = new GenerateDocumentService(
            documents,
            templates,
            unitOfWork,
            objectStorage,
            new FakeVerificationIdGenerator(),
            pipeline,
            new FakeNotificationRequestPublisher(),
            clock,
            NullLogger<GenerateDocumentService>.Instance);

        return (service, documents, templates);
    }

    [Fact]
    public async Task GenerateAsync_without_a_published_template_returns_not_found()
    {
        var (service, _, _) = BuildService();
        var command = new GenerateDocumentCommand(Guid.NewGuid(), DocumentType.Transcript, Guid.NewGuid(), new Dictionary<string, string>(), LanguageCode.En, Guid.NewGuid(), "corr-1");

        var result = await service.GenerateAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal("document.template_not_found", result.Error!.Code);
    }

    [Fact]
    public async Task GenerateAsync_a_new_natural_key_renders_and_reaches_Ready()
    {
        var (service, _, templates) = BuildService();
        templates.Add(PublishTemplate());
        var command = new GenerateDocumentCommand(Guid.NewGuid(), DocumentType.Transcript, Guid.NewGuid(), new Dictionary<string, string> { ["studentName"] = "Rahim" }, LanguageCode.En, Guid.NewGuid(), "corr-1");

        var result = await service.GenerateAsync(command);

        Assert.True(result.IsSuccess);
        Assert.Equal("Ready", result.Value.Status);
        Assert.NotNull(result.Value.DownloadUrl);
    }

    [Fact]
    public async Task GenerateAsync_retried_with_the_identical_natural_key_returns_the_same_document_idempotently()
    {
        var (service, _, templates) = BuildService();
        templates.Add(PublishTemplate());
        var ownerId = Guid.NewGuid();
        var sourceReferenceId = Guid.NewGuid();
        var command = new GenerateDocumentCommand(ownerId, DocumentType.Transcript, sourceReferenceId, new Dictionary<string, string>(), LanguageCode.En, Guid.NewGuid(), "corr-1");

        var first = await service.GenerateAsync(command);
        var second = await service.GenerateAsync(command with { CorrelationId = "corr-2" });

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value.Id, second.Value.Id);
        Assert.Equal("Ready", second.Value.Status);
    }

    [Fact]
    public async Task GenerateAsync_for_different_owners_never_collides_on_the_natural_key()
    {
        var (service, _, templates) = BuildService();
        templates.Add(PublishTemplate());
        var sourceReferenceId = Guid.NewGuid();
        var commandA = new GenerateDocumentCommand(Guid.NewGuid(), DocumentType.Transcript, sourceReferenceId, new Dictionary<string, string>(), LanguageCode.En, Guid.NewGuid(), "corr-a");
        var commandB = new GenerateDocumentCommand(Guid.NewGuid(), DocumentType.Transcript, sourceReferenceId, new Dictionary<string, string>(), LanguageCode.En, Guid.NewGuid(), "corr-b");

        var resultA = await service.GenerateAsync(commandA);
        var resultB = await service.GenerateAsync(commandB);

        Assert.True(resultA.IsSuccess);
        Assert.True(resultB.IsSuccess);
        Assert.NotEqual(resultA.Value.Id, resultB.Value.Id);
    }

    private static Domain.Templates.DocumentTemplate PublishTemplate()
    {
        var translations = new Dictionary<LanguageCode, (string Title, string LabelsJson)>
        {
            [LanguageCode.En] = ("Transcript", "{}"),
        };
        return Domain.Templates.DocumentTemplate.Create(DocumentType.Transcript, 1, null, translations, DateTimeOffset.UtcNow).Value;
    }
}

using UMS.Modules.Documents.Domain.BulkJobs;
using UMS.Modules.Documents.Domain.Common;
using UMS.Modules.Documents.Domain.Templates;

namespace UMS.Modules.Documents.UnitTests.BulkJobs;

/// <summary>
/// DOC-4/DOC-6: the ADR-0014 job shape - template-version pinning at creation, and the
/// counters/completion state edge-cases.md's per-item resumability decision depends on.
/// requirement-spec.md documents §4's "a request for more than one document ... rejected on the
/// synchronous endpoint" is enforced structurally by <c>GenerateDocumentCommand</c>'s single-item
/// shape (Application layer), not here - this aggregate is the bulk path's own counterpart, so its
/// own invariant is the opposite direction: at least one item is required (a single-item request
/// belongs on the synchronous endpoint instead).
/// </summary>
public sealed class BulkGenerationJobTests
{
    [Fact]
    public void Create_with_zero_items_is_rejected()
    {
        var result = BulkGenerationJob.Create(DocumentType.AdmitCard, DocumentTemplateId.New(), templateVersion: 1, Guid.NewGuid(), totalItems: 0, DateTimeOffset.UtcNow);

        Assert.True(result.IsFailure);
        Assert.Equal("bulk_generation_job.items_required", result.Error!.Code);
    }

    [Fact]
    public void Create_pins_the_template_version_given_at_creation_time()
    {
        var result = BulkGenerationJob.Create(DocumentType.AdmitCard, DocumentTemplateId.New(), templateVersion: 3, Guid.NewGuid(), totalItems: 10, DateTimeOffset.UtcNow);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.TemplateVersion);
        Assert.Equal(BulkGenerationJobStatus.Pending, result.Value.Status);
    }

    [Fact]
    public void RecordItemOutcome_increments_completed_count_and_dead_lettered_count_independently()
    {
        var job = BulkGenerationJob.Create(DocumentType.AdmitCard, DocumentTemplateId.New(), 1, Guid.NewGuid(), totalItems: 3, DateTimeOffset.UtcNow).Value;

        job.RecordItemOutcome(deadLettered: false);
        job.RecordItemOutcome(deadLettered: true);

        Assert.Equal(2, job.CompletedCount);
        Assert.Equal(1, job.DeadLetteredCount);
    }

    [Fact]
    public void Complete_before_every_item_has_resolved_is_rejected()
    {
        var job = BulkGenerationJob.Create(DocumentType.AdmitCard, DocumentTemplateId.New(), 1, Guid.NewGuid(), totalItems: 3, DateTimeOffset.UtcNow).Value;
        job.RecordItemOutcome(deadLettered: false);

        var result = job.Complete(DateTimeOffset.UtcNow);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Complete_with_no_dead_letters_reaches_Completed()
    {
        var job = BulkGenerationJob.Create(DocumentType.AdmitCard, DocumentTemplateId.New(), 1, Guid.NewGuid(), totalItems: 2, DateTimeOffset.UtcNow).Value;
        job.RecordItemOutcome(deadLettered: false);
        job.RecordItemOutcome(deadLettered: false);

        var result = job.Complete(DateTimeOffset.UtcNow);

        Assert.True(result.IsSuccess);
        Assert.Equal(BulkGenerationJobStatus.Completed, job.Status);
        Assert.NotNull(job.CompletedAt);
    }

    [Fact]
    public void Complete_with_at_least_one_dead_letter_reaches_CompletedWithErrors()
    {
        var job = BulkGenerationJob.Create(DocumentType.AdmitCard, DocumentTemplateId.New(), 1, Guid.NewGuid(), totalItems: 2, DateTimeOffset.UtcNow).Value;
        job.RecordItemOutcome(deadLettered: true);
        job.RecordItemOutcome(deadLettered: false);

        var result = job.Complete(DateTimeOffset.UtcNow);

        Assert.True(result.IsSuccess);
        Assert.Equal(BulkGenerationJobStatus.CompletedWithErrors, job.Status);
    }

    [Fact]
    public void MarkProcessing_on_an_already_terminal_job_is_rejected()
    {
        var job = BulkGenerationJob.Create(DocumentType.AdmitCard, DocumentTemplateId.New(), 1, Guid.NewGuid(), totalItems: 1, DateTimeOffset.UtcNow).Value;
        job.RecordItemOutcome(deadLettered: false);
        job.Complete(DateTimeOffset.UtcNow);

        var result = job.MarkProcessing();

        Assert.True(result.IsFailure);
    }
}

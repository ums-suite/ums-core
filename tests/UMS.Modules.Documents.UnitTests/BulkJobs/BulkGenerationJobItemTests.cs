using UMS.Modules.Documents.Domain.BulkJobs;
using UMS.Modules.Documents.Domain.GeneratedDocuments;

namespace UMS.Modules.Documents.UnitTests.BulkJobs;

/// <summary>edge-cases.md's per-item checkpoint/dead-letter decision: "chunked, individually retryable, and dead-lettered per-item - one bad record never fails the whole batch."</summary>
public sealed class BulkGenerationJobItemTests
{
    private static BulkGenerationJobItem NewItem() =>
        BulkGenerationJobItem.Create(BulkGenerationJobId.New(), Guid.NewGuid(), Guid.NewGuid(), "{}");

    [Fact]
    public void Create_starts_Pending_with_zero_attempts()
    {
        var item = NewItem();

        Assert.Equal(BulkGenerationJobItemStatus.Pending, item.Status);
        Assert.Equal(0, item.AttemptCount);
    }

    [Fact]
    public void MarkCompleted_records_the_resulting_document_id()
    {
        var item = NewItem();
        var documentId = GeneratedDocumentId.New();

        item.MarkCompleted(documentId);

        Assert.Equal(BulkGenerationJobItemStatus.Completed, item.Status);
        Assert.Equal(documentId, item.GeneratedDocumentId);
    }

    [Fact]
    public void RecordFailure_below_the_retry_budget_returns_to_Pending_for_a_retry()
    {
        var item = NewItem();
        item.MarkProcessing();

        var deadLettered = item.RecordFailure("transient upload failure");

        Assert.False(deadLettered);
        Assert.Equal(BulkGenerationJobItemStatus.Pending, item.Status);
    }

    [Fact]
    public void RecordFailure_after_exhausting_the_retry_budget_dead_letters_the_item()
    {
        var item = NewItem();

        item.MarkProcessing();
        item.RecordFailure("attempt 1");
        item.MarkProcessing();
        item.RecordFailure("attempt 2");
        item.MarkProcessing();
        var deadLettered = item.RecordFailure("attempt 3");

        Assert.True(deadLettered);
        Assert.Equal(BulkGenerationJobItemStatus.DeadLettered, item.Status);
        Assert.Equal("attempt 3", item.ErrorMessage);
    }

    [Fact]
    public void MarkProcessing_on_an_already_completed_item_is_rejected()
    {
        var item = NewItem();
        item.MarkCompleted(GeneratedDocumentId.New());

        var result = item.MarkProcessing();

        Assert.True(result.IsFailure);
    }
}

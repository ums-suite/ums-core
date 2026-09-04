using UMS.Modules.Documents.Domain.BulkJobs;

namespace UMS.Modules.Documents.Application.BulkJobs;

public sealed record BulkGenerationJobDto(
    Guid Id,
    string DocumentType,
    Guid TemplateId,
    int TemplateVersion,
    string Status,
    int TotalItems,
    int CompletedCount,
    int DeadLetteredCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt)
{
    public static BulkGenerationJobDto FromDomain(BulkGenerationJob job) => new(
        job.Id.Value,
        job.DocumentType.ToString(),
        job.TemplateId.Value,
        job.TemplateVersion,
        job.Status.ToString(),
        job.TotalItems,
        job.CompletedCount,
        job.DeadLetteredCount,
        job.CreatedAt,
        job.CompletedAt);
}

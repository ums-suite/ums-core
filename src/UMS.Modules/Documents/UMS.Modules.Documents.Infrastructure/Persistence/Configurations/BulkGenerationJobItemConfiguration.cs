using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Documents.Domain.BulkJobs;
using UMS.Modules.Documents.Domain.GeneratedDocuments;

namespace UMS.Modules.Documents.Infrastructure.Persistence.Configurations;

/// <summary>
/// design-decisions.md's per-item checkpoint decision. Uses <see cref="Microsoft.EntityFrameworkCore.PropertyAccessMode"/>
/// concurrency defaults (last-write-wins) deliberately, not optimistic-concurrency tokens: at
/// ~250,000-item scale (§8), only one worker instance processes a given job in this v1 (a known,
/// documented scaling gap - see the PR description) so cross-worker write races are not yet a
/// concern this configuration needs to defend against.
/// </summary>
internal sealed class BulkGenerationJobItemConfiguration : IEntityTypeConfiguration<BulkGenerationJobItem>
{
    public void Configure(EntityTypeBuilder<BulkGenerationJobItem> builder)
    {
        builder.ToTable("bulk_generation_job_items");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id)
            .HasConversion(id => id.Value, value => new BulkGenerationJobItemId(value))
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(i => i.JobId)
            .HasConversion(id => id.Value, value => new BulkGenerationJobId(value))
            .HasColumnName("job_id")
            .IsRequired();

        builder.Property(i => i.OwnerId).HasColumnName("owner_id").IsRequired();
        builder.Property(i => i.SourceReferenceId).HasColumnName("source_reference_id").IsRequired();
        builder.Property(i => i.RenderDataJson).HasColumnName("render_data_json").HasColumnType("jsonb").IsRequired();
        builder.Property(i => i.Status).HasColumnName("status").HasConversion<string>().IsRequired();
        builder.Property(i => i.AttemptCount).HasColumnName("attempt_count").IsRequired();

        builder.Property(i => i.GeneratedDocumentId)
            .HasConversion(
                id => id == null ? (Guid?)null : id.Value.Value,
                value => value == null ? (GeneratedDocumentId?)null : new GeneratedDocumentId(value.Value))
            .HasColumnName("generated_document_id");

        builder.Property(i => i.ErrorMessage).HasColumnName("error_message");

        // The bulk relay worker's own resumability read shape (edge-cases.md): "give me this job's
        // not-yet-resolved items."
        builder.HasIndex(i => new { i.JobId, i.Status }).HasDatabaseName("ix_bulk_generation_job_items_job_status");
    }
}

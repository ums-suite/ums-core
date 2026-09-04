using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Documents.Domain.BulkJobs;
using UMS.Modules.Documents.Domain.Templates;

namespace UMS.Modules.Documents.Infrastructure.Persistence.Configurations;

internal sealed class BulkGenerationJobConfiguration : IEntityTypeConfiguration<BulkGenerationJob>
{
    public void Configure(EntityTypeBuilder<BulkGenerationJob> builder)
    {
        builder.ToTable("bulk_generation_jobs");
        builder.HasKey(j => j.Id);

        builder.Property(j => j.Id)
            .HasConversion(id => id.Value, value => new BulkGenerationJobId(value))
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(j => j.DocumentType).HasColumnName("document_type").HasConversion<string>().IsRequired();

        builder.Property(j => j.TemplateId)
            .HasConversion(id => id.Value, value => new DocumentTemplateId(value))
            .HasColumnName("template_id")
            .IsRequired();

        builder.Property(j => j.TemplateVersion).HasColumnName("template_version").IsRequired();
        builder.Property(j => j.RequestedByUserId).HasColumnName("requested_by_user_id").IsRequired();
        builder.Property(j => j.Status).HasColumnName("status").HasConversion<string>().IsRequired();
        builder.Property(j => j.TotalItems).HasColumnName("total_items").IsRequired();
        builder.Property(j => j.CompletedCount).HasColumnName("completed_count").IsRequired();
        builder.Property(j => j.DeadLetteredCount).HasColumnName("dead_lettered_count").IsRequired();
        builder.Property(j => j.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(j => j.CompletedAt).HasColumnName("completed_at");

        builder.HasIndex(j => j.Status).HasDatabaseName("ix_bulk_generation_jobs_status");
    }
}

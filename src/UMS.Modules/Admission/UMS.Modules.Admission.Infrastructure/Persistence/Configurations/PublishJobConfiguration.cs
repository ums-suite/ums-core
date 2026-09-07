using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Admission.Domain.Publishing;

namespace UMS.Modules.Admission.Infrastructure.Persistence.Configurations;

internal sealed class PublishJobConfiguration : IEntityTypeConfiguration<PublishJob>
{
    public void Configure(EntityTypeBuilder<PublishJob> builder)
    {
        builder.ToTable("publish_jobs");
        builder.HasKey(j => j.Id);
        builder.Property(j => j.Id).HasConversion(id => id.Value, value => new PublishJobId(value)).ValueGeneratedNever().HasColumnName("id");

        builder.Property(j => j.AdmissionResultId).HasColumnName("admission_result_id").IsRequired();
        builder.Property(j => j.CampaignId).HasColumnName("campaign_id").IsRequired();
        builder.Property(j => j.Stage).HasColumnName("stage").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(j => j.TotalCount).HasColumnName("total_count").IsRequired();
        builder.Property(j => j.CacheProcessedCount).HasColumnName("cache_processed_count").IsRequired();
        builder.Property(j => j.CacheCursor).HasColumnName("cache_cursor");
        builder.Property(j => j.FanOutProcessedCount).HasColumnName("fan_out_processed_count").IsRequired();
        builder.Property(j => j.FanOutCursor).HasColumnName("fan_out_cursor");
        builder.Property(j => j.StartedAt).HasColumnName("started_at").IsRequired();
        builder.Property(j => j.CompletedAt).HasColumnName("completed_at");
        builder.Property(j => j.FailureReason).HasColumnName("failure_reason").HasMaxLength(2000);
        builder.Property(j => j.Version).HasColumnName("xmin").HasColumnType("xid").IsRowVersion().ValueGeneratedOnAddOrUpdate();

        builder.HasIndex(j => j.AdmissionResultId).HasDatabaseName("ix_publish_jobs_admission_result_id");
        builder.HasIndex(j => j.Stage).HasDatabaseName("ix_publish_jobs_stage");
    }
}

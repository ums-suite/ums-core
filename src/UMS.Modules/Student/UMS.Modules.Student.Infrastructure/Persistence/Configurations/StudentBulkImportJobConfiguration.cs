using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Student.Domain.BulkImport;

namespace UMS.Modules.Student.Infrastructure.Persistence.Configurations;

internal sealed class StudentBulkImportJobConfiguration : IEntityTypeConfiguration<StudentBulkImportJob>
{
    public void Configure(EntityTypeBuilder<StudentBulkImportJob> builder)
    {
        builder.ToTable("student_bulk_import_jobs");
        builder.HasKey(j => j.Id);
        builder.Property(j => j.Id)
            .HasConversion(id => id.Value, value => new StudentBulkImportJobId(value))
            .ValueGeneratedNever()
            .HasColumnName("id");

        builder.Property(j => j.RequestedByUserId).HasColumnName("requested_by_user_id").IsRequired();
        builder.Property(j => j.Status).HasColumnName("status").HasConversion<string>().IsRequired();
        builder.Property(j => j.TotalRows).HasColumnName("total_rows").IsRequired();
        builder.Property(j => j.ValidRowCount).HasColumnName("valid_row_count").IsRequired();
        builder.Property(j => j.InvalidRowCount).HasColumnName("invalid_row_count").IsRequired();
        builder.Property(j => j.ProcessedCount).HasColumnName("processed_count").IsRequired();
        builder.Property(j => j.SucceededCount).HasColumnName("succeeded_count").IsRequired();
        builder.Property(j => j.FailedCount).HasColumnName("failed_count").IsRequired();
        builder.Property(j => j.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(j => j.ApprovedAt).HasColumnName("approved_at");
        builder.Property(j => j.CompletedAt).HasColumnName("completed_at");

        builder.HasIndex(j => j.Status).HasDatabaseName("ix_student_bulk_import_jobs_status");
    }
}

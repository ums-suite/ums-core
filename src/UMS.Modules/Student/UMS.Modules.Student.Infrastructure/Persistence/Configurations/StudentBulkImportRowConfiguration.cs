using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Student.Domain.BulkImport;

namespace UMS.Modules.Student.Infrastructure.Persistence.Configurations;

/// <summary>Its own independently-queryable table, not an owned collection of <see cref="StudentBulkImportJob"/> - see that aggregate's own remarks (mirrors Documents' own <c>BulkGenerationJobItem</c> exactly).</summary>
internal sealed class StudentBulkImportRowConfiguration : IEntityTypeConfiguration<StudentBulkImportRow>
{
    public void Configure(EntityTypeBuilder<StudentBulkImportRow> builder)
    {
        builder.ToTable("student_bulk_import_rows");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id)
            .HasConversion(id => id.Value, value => new StudentBulkImportRowId(value))
            .ValueGeneratedNever()
            .HasColumnName("id");

        builder.Property(r => r.JobId)
            .HasConversion(id => id.Value, value => new StudentBulkImportJobId(value))
            .HasColumnName("job_id")
            .IsRequired();

        builder.Property(r => r.RowNumber).HasColumnName("row_number").IsRequired();
        builder.Property(r => r.PayloadJson).HasColumnName("payload_json").IsRequired();
        builder.Property(r => r.Status).HasColumnName("status").HasConversion<string>().IsRequired();
        builder.Property(r => r.ErrorMessage).HasColumnName("error_message");
        builder.Property(r => r.ResultStudentId).HasColumnName("result_student_id");

        builder.HasIndex(r => new { r.JobId, r.Status }).HasDatabaseName("ix_student_bulk_import_rows_job_status");
        builder.HasIndex(r => new { r.JobId, r.RowNumber }).IsUnique().HasDatabaseName("ux_student_bulk_import_rows_job_row_number");
    }
}

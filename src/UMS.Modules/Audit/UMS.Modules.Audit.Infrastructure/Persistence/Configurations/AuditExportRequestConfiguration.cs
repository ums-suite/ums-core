using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Audit.Domain.Exports;

namespace UMS.Modules.Audit.Infrastructure.Persistence.Configurations;

internal sealed class AuditExportRequestConfiguration : IEntityTypeConfiguration<AuditExportRequest>
{
    public void Configure(EntityTypeBuilder<AuditExportRequest> builder)
    {
        builder.ToTable("audit_export_requests");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(r => r.RequestedByUserId).HasColumnName("requested_by_user_id").IsRequired();
        builder.Property(r => r.FilterJson).HasColumnName("filter_json").HasColumnType("jsonb").IsRequired();
        builder.Property(r => r.Format).HasColumnName("format").HasConversion<string>().IsRequired();
        builder.Property(r => r.Status).HasColumnName("status").HasConversion<string>().IsRequired();
        builder.Property(r => r.RequestedAt).HasColumnName("requested_at").IsRequired();
        builder.Property(r => r.CompletedAt).HasColumnName("completed_at");
        builder.Property(r => r.ResultObjectKey).HasColumnName("result_object_key");
        builder.Property(r => r.ErrorMessage).HasColumnName("error_message");

        builder.HasIndex(r => r.Status).HasDatabaseName("ix_audit_export_requests_status");
    }
}

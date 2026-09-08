using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Reporting.Domain.RegulatoryReports;

namespace UMS.Modules.Reporting.Infrastructure.Persistence.Configurations;

internal sealed class RegulatoryReportRunConfiguration : IEntityTypeConfiguration<RegulatoryReportRun>
{
    public void Configure(EntityTypeBuilder<RegulatoryReportRun> builder)
    {
        builder.ToTable("regulatory_report_runs");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasConversion(id => id.Value, value => new RegulatoryReportRunId(value)).ValueGeneratedNever().HasColumnName("id");

        builder.Property(r => r.DefinitionId).HasConversion(id => id.Value, value => new RegulatoryReportDefinitionId(value)).HasColumnName("definition_id").IsRequired();
        builder.Property(r => r.DefinitionSnapshotJson).HasColumnName("definition_snapshot_json").HasColumnType("jsonb").IsRequired();
        builder.Property(r => r.ParametersJson).HasColumnName("parameters_json").HasColumnType("jsonb").IsRequired();
        builder.Property(r => r.ParametersHash).HasColumnName("parameters_hash").HasMaxLength(64).IsRequired();
        builder.Property(r => r.Format).HasColumnName("format").HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(r => r.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(r => r.RequestedByUserId).HasColumnName("requested_by_user_id").IsRequired();
        builder.Property(r => r.RequestedAt).HasColumnName("requested_at").IsRequired();
        builder.Property(r => r.AsOf).HasColumnName("as_of");
        builder.Property(r => r.StartedAt).HasColumnName("started_at");
        builder.Property(r => r.CompletedAt).HasColumnName("completed_at");
        builder.Property(r => r.ResultDocumentId).HasColumnName("result_document_id");
        builder.Property(r => r.ResultCsvContent).HasColumnName("result_csv_content");
        builder.Property(r => r.ErrorMessage).HasColumnName("error_message");
        builder.Property(r => r.Version).HasColumnName("xmin").HasColumnType("xid").IsRowVersion().ValueGeneratedOnAddOrUpdate();

        // RPT-12's own polling queue (see RegulatoryReportRunService's own remarks) - the relay
        // worker's candidate query.
        builder.HasIndex(r => r.Status).HasDatabaseName("ix_regulatory_report_runs_status");

        // RPT-15's courtesy-notice lookup: the most recent non-terminal run sharing a parameters hash.
        builder.HasIndex(r => new { r.ParametersHash, r.Status }).HasDatabaseName("ix_regulatory_report_runs_parameters_hash_status");
    }
}

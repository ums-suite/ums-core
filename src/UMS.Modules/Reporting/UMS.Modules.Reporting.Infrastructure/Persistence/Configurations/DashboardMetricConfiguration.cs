using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Reporting.Domain.DashboardMetrics;

namespace UMS.Modules.Reporting.Infrastructure.Persistence.Configurations;

internal sealed class DashboardMetricConfiguration : IEntityTypeConfiguration<DashboardMetric>
{
    public void Configure(EntityTypeBuilder<DashboardMetric> builder)
    {
        builder.ToTable("dashboard_metrics");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("metric_key").HasMaxLength(100).ValueGeneratedNever();

        builder.Property(m => m.DisplayName).HasColumnName("display_name").HasMaxLength(200).IsRequired();
        builder.Property(m => m.PayloadJson).HasColumnName("payload_json").HasColumnType("jsonb");
        builder.Property(m => m.DataAsOf).HasColumnName("data_as_of");
        builder.Property(m => m.LastRefreshAttemptedAt).HasColumnName("last_refresh_attempted_at");
        builder.Property(m => m.LastRefreshSucceeded).HasColumnName("last_refresh_succeeded").IsRequired();
        builder.Property(m => m.LastRefreshError).HasColumnName("last_refresh_error");
        builder.Property(m => m.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(m => m.Version).HasColumnName("xmin").HasColumnType("xid").IsRowVersion().ValueGeneratedOnAddOrUpdate();
    }
}

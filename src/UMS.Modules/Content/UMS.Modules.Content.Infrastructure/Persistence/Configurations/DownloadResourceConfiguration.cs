using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Content.Domain.Downloads;

namespace UMS.Modules.Content.Infrastructure.Persistence.Configurations;

/// <summary>CNT-11: `download_resources` - metadata + a `Documents` artifact reference only, never a blob.</summary>
internal sealed class DownloadResourceConfiguration : IEntityTypeConfiguration<DownloadResource>
{
    public void Configure(EntityTypeBuilder<DownloadResource> builder)
    {
        builder.ToTable("download_resources");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id)
            .HasConversion(id => id.Value, value => new DownloadResourceId(value))
            .ValueGeneratedNever()
            .HasColumnName("id");

        builder.Property(d => d.Title).HasColumnName("title").IsRequired();
        builder.Property(d => d.Category).HasColumnName("category").HasMaxLength(100).IsRequired();
        builder.Property(d => d.ArtifactId).HasColumnName("artifact_id").IsRequired();
        builder.Property(d => d.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(d => d.PublishAt).HasColumnName("publish_at");
        builder.Property(d => d.ExpireAt).HasColumnName("expire_at");
        builder.Property(d => d.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        builder.Property(d => d.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(d => d.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.Property(d => d.Version)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsRowVersion()
            .ValueGeneratedOnAddOrUpdate();

        builder.HasIndex(d => new { d.Status, d.PublishAt }).HasDatabaseName("ix_download_resources_status_publish_at");
        builder.HasIndex(d => new { d.Status, d.ExpireAt }).HasDatabaseName("ix_download_resources_status_expire_at");
        builder.HasIndex(d => d.Category).HasDatabaseName("ix_download_resources_category");
    }
}

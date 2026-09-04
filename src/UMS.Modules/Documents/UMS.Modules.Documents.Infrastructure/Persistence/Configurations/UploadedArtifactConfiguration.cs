using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Documents.Domain.UploadedArtifacts;

namespace UMS.Modules.Documents.Infrastructure.Persistence.Configurations;

internal sealed class UploadedArtifactConfiguration : IEntityTypeConfiguration<UploadedArtifact>
{
    public void Configure(EntityTypeBuilder<UploadedArtifact> builder)
    {
        builder.ToTable("uploaded_artifacts");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasConversion(id => id.Value, value => new UploadedArtifactId(value))
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(a => a.OwnerId).HasColumnName("owner_id").IsRequired();
        builder.Property(a => a.ArtifactType).HasColumnName("artifact_type").IsRequired();
        builder.Property(a => a.MimeType).HasColumnName("mime_type").IsRequired();
        builder.Property(a => a.StorageKey).HasColumnName("storage_key").IsRequired();
        builder.Property(a => a.Checksum).HasColumnName("checksum");
        builder.Property(a => a.SizeBytes).HasColumnName("size_bytes");
        builder.Property(a => a.Status).HasColumnName("status").HasConversion<string>().IsRequired();
        builder.Property(a => a.RequestedAt).HasColumnName("requested_at").IsRequired();
        builder.Property(a => a.ReadyAt).HasColumnName("ready_at");
        builder.Property(a => a.FailureReason).HasColumnName("failure_reason");

        builder.HasIndex(a => a.StorageKey).IsUnique().HasDatabaseName("ux_uploaded_artifacts_storage_key");
        builder.HasIndex(a => new { a.OwnerId, a.ArtifactType }).HasDatabaseName("ix_uploaded_artifacts_owner_type");
    }
}

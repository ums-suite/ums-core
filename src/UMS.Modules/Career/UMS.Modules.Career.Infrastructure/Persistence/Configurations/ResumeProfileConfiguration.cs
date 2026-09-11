using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Career.Domain.ResumeProfiles;

namespace UMS.Modules.Career.Infrastructure.Persistence.Configurations;

/// <summary>requirement-spec.md §2.7: exactly one `ResumeProfile` per Student is flagged default at any time - enforced by a partial unique index, not merely in-process.</summary>
internal sealed class ResumeProfileConfiguration : IEntityTypeConfiguration<ResumeProfile>
{
    public void Configure(EntityTypeBuilder<ResumeProfile> builder)
    {
        builder.ToTable("resume_profiles");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasConversion(id => id.Value, value => new ResumeProfileId(value)).ValueGeneratedNever().HasColumnName("id");

        builder.Property(r => r.StudentId).HasColumnName("student_id").IsRequired();
        builder.Property(r => r.Label).HasColumnName("label").HasMaxLength(200).IsRequired();
        builder.Property(r => r.ArtifactId).HasColumnName("artifact_id").IsRequired();
        builder.Property(r => r.FileName).HasColumnName("file_name").HasMaxLength(500).IsRequired();
        builder.Property(r => r.IsDefault).HasColumnName("is_default").IsRequired();
        builder.Property(r => r.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(r => r.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(r => r.Version).HasColumnName("xmin").HasColumnType("xid").IsRowVersion().ValueGeneratedOnAddOrUpdate();

        builder.HasIndex(r => r.StudentId).HasDatabaseName("ix_resume_profiles_student_id");

        builder.HasIndex(r => new { r.StudentId, r.IsDefault })
            .HasDatabaseName("ux_resume_profiles_student_default")
            .IsUnique()
            .HasFilter("is_default = true AND is_deleted = false");
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Alumni.Domain.Jobs;

namespace UMS.Modules.Alumni.Infrastructure.Persistence.Configurations;

internal sealed class JobPostingConfiguration : IEntityTypeConfiguration<JobPosting>
{
    public void Configure(EntityTypeBuilder<JobPosting> builder)
    {
        builder.ToTable("job_postings");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasConversion(id => id.Value, value => new JobPostingId(value)).ValueGeneratedNever().HasColumnName("id");

        builder.Property(p => p.PosterUserId).HasColumnName("poster_user_id").IsRequired();
        builder.Property(p => p.PosterIsAlumnus).HasColumnName("poster_is_alumnus").IsRequired();
        builder.Property(p => p.PosterAlumnusId).HasColumnName("poster_alumnus_id");
        builder.Property(p => p.Title).HasColumnName("title").HasMaxLength(300).IsRequired();
        builder.Property(p => p.Company).HasColumnName("company").HasMaxLength(300);
        builder.Property(p => p.Description).HasColumnName("description").HasMaxLength(8000);
        builder.Property(p => p.Location).HasColumnName("location").HasMaxLength(300);
        builder.Property(p => p.ContactMethod).HasColumnName("contact_method").HasMaxLength(500);
        builder.Property(p => p.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.Property(p => p.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(p => p.ModerationReason).HasColumnName("moderation_reason").HasMaxLength(1000);
        builder.Property(p => p.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(p => p.PublishedAt).HasColumnName("published_at");
        builder.Property(p => p.Version).HasColumnName("xmin").HasColumnType("xid").IsRowVersion().ValueGeneratedOnAddOrUpdate();

        builder.HasIndex(p => p.Status).HasDatabaseName("ix_job_postings_status");
        builder.HasIndex(p => p.PosterUserId).HasDatabaseName("ix_job_postings_poster_user_id");
        builder.HasIndex(p => new { p.Status, p.ExpiresAt }).HasDatabaseName("ix_job_postings_status_expires_at");
    }
}

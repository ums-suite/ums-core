using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Alumni.Domain.Jobs;

namespace UMS.Modules.Alumni.Infrastructure.Persistence.Configurations;

internal sealed class JobApplicationConfiguration : IEntityTypeConfiguration<JobApplication>
{
    public void Configure(EntityTypeBuilder<JobApplication> builder)
    {
        builder.ToTable("job_applications");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasConversion(id => id.Value, value => new JobApplicationId(value)).ValueGeneratedNever().HasColumnName("id");

        builder.Property(a => a.JobPostingId).HasConversion(id => id.Value, value => new JobPostingId(value)).HasColumnName("job_posting_id").IsRequired();
        builder.Property(a => a.ApplicantUserId).HasColumnName("applicant_user_id").IsRequired();
        builder.Property(a => a.ApplicantIsAlumnus).HasColumnName("applicant_is_alumnus").IsRequired();
        builder.Property(a => a.Note).HasColumnName("note").HasMaxLength(2000);
        builder.Property(a => a.ResumeArtifactId).HasColumnName("resume_artifact_id");
        builder.Property(a => a.SubmittedAt).HasColumnName("submitted_at").IsRequired();

        builder.HasIndex(a => a.JobPostingId).HasDatabaseName("ix_job_applications_job_posting_id");
        builder.HasIndex(a => a.ApplicantUserId).HasDatabaseName("ix_job_applications_applicant_user_id");
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Career.Domain.Applications;
using UMS.Modules.Career.Domain.Drives;
using UMS.Modules.Career.Domain.Internships;

namespace UMS.Modules.Career.Infrastructure.Persistence.Configurations;

/// <summary>
/// requirement-spec.md §4: two DB-level invariants, both defense-in-depth alongside the application
/// layer's own guards -
/// (1) `internship_id` XOR `drive_id` (<c>ck_career_applications_target_xor</c>) - a `CareerApplication`
/// always targets exactly one of the two, never both or neither;
/// (2) at most one non-Withdrawn/non-Cancelled `CareerApplication` per (Student, Internship), and
/// separately per (Student, Drive) - two partial unique indexes, one per target column.
/// </summary>
internal sealed class CareerApplicationConfiguration : IEntityTypeConfiguration<CareerApplication>
{
    public void Configure(EntityTypeBuilder<CareerApplication> builder)
    {
        builder.ToTable("career_applications", t => t.HasCheckConstraint(
            "ck_career_applications_target_xor",
            "(internship_id IS NOT NULL AND drive_id IS NULL) OR (internship_id IS NULL AND drive_id IS NOT NULL)"));
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasConversion(id => id.Value, value => new CareerApplicationId(value)).ValueGeneratedNever().HasColumnName("id");

        builder.Property(a => a.StudentId).HasColumnName("student_id").IsRequired();
        builder.Property(a => a.InternshipId).HasConversion(id => id!.Value.Value, value => new InternshipId(value)).HasColumnName("internship_id");
        builder.Property(a => a.DriveId).HasConversion(id => id!.Value.Value, value => new CampusRecruitmentDriveId(value)).HasColumnName("drive_id");
        builder.Property(a => a.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(a => a.DeclaredCgpa).HasColumnName("declared_cgpa").HasColumnType("numeric(3,2)");
        builder.Property(a => a.DeclaredYearOfStudy).HasColumnName("declared_year_of_study");
        builder.Property(a => a.ResumeProfileIdSnapshot).HasColumnName("resume_profile_id_snapshot").IsRequired();
        builder.Property(a => a.ResumeArtifactIdSnapshot).HasColumnName("resume_artifact_id_snapshot").IsRequired();
        builder.Property(a => a.ResumeFileNameSnapshot).HasColumnName("resume_file_name_snapshot").HasMaxLength(500).IsRequired();
        builder.Property(a => a.ResumeSnapshotAt).HasColumnName("resume_snapshot_at").IsRequired();
        builder.Property(a => a.InterviewSlotId).HasColumnName("interview_slot_id");
        builder.Property(a => a.SubmittedAt).HasColumnName("submitted_at").IsRequired();
        builder.Property(a => a.DecisionReason).HasColumnName("decision_reason").HasMaxLength(2000);
        builder.Property(a => a.DecidedAt).HasColumnName("decided_at");
        builder.Property(a => a.Version).HasColumnName("xmin").HasColumnType("xid").IsRowVersion().ValueGeneratedOnAddOrUpdate();

        builder.HasIndex(a => a.StudentId).HasDatabaseName("ix_career_applications_student_id");
        builder.HasIndex(a => a.InternshipId).HasDatabaseName("ix_career_applications_internship_id");
        builder.HasIndex(a => a.DriveId).HasDatabaseName("ix_career_applications_drive_id");

        builder.HasIndex(a => new { a.StudentId, a.InternshipId })
            .HasDatabaseName("ux_career_applications_student_internship")
            .IsUnique()
            .HasFilter("internship_id IS NOT NULL AND status NOT IN ('Withdrawn', 'Cancelled')");

        builder.HasIndex(a => new { a.StudentId, a.DriveId })
            .HasDatabaseName("ux_career_applications_student_drive")
            .IsUnique()
            .HasFilter("drive_id IS NOT NULL AND status NOT IN ('Withdrawn', 'Cancelled')");
    }
}

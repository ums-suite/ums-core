using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Academic.Domain.Enrollments;

namespace UMS.Modules.Academic.Infrastructure.Persistence.Configurations;

internal sealed class EnrollmentConfiguration : IEntityTypeConfiguration<Enrollment>
{
    public void Configure(EntityTypeBuilder<Enrollment> builder)
    {
        builder.ToTable("enrollments");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasConversion(id => id.Value, value => new EnrollmentId(value)).ValueGeneratedNever().HasColumnName("id");

        builder.Property(e => e.StudentId).HasColumnName("student_id").IsRequired();
        builder.Property(e => e.CourseOfferingId).HasColumnName("course_offering_id").IsRequired();
        builder.Property(e => e.SemesterId).HasColumnName("semester_id").IsRequired();
        builder.Property(e => e.SectionId).HasColumnName("section_id").IsRequired();

        // Deliberately a PARTIAL unique index, not a plain one: requirement-spec.md §4's
        // "exactly one Enrollment per (Student, CourseOffering, Semester)" invariant is about the
        // current roster, not all of history - a genuine bug caught during this flow's manual
        // end-to-end verification found that an unconditional unique index makes a real
        // drop-then-re-enroll-in-the-SAME-offering (a legitimate flow requirement-spec.md §2's own
        // drop-window language assumes exists) permanently impossible after the first drop: every
        // later POST /enrollments for that exact tuple collides with the now-Dropped row and the
        // duplicate-submission catch path (below) silently hands back the stale Dropped DTO forever,
        // never a fresh Active one. Excluding 'Dropped' rows from the constraint lets a fresh row be
        // inserted after a drop while still blocking a genuine duplicate/double-click against an
        // already-Active-or-Pending row for the same tuple - the case this index exists to prevent.
        builder.HasIndex(e => new { e.StudentId, e.CourseOfferingId, e.SemesterId })
            .IsUnique()
            .HasFilter("status <> 'Dropped'")
            .HasDatabaseName("ux_enrollments_student_courseoffering_semester");
        builder.HasIndex(e => new { e.StudentId, e.SemesterId }).HasDatabaseName("ix_enrollments_student_semester");
        builder.HasIndex(e => e.CourseOfferingId).HasDatabaseName("ix_enrollments_course_offering_id");

        builder.Property(e => e.Status).HasColumnName("status").HasConversion<string>().IsRequired();
        builder.Property(e => e.PrerequisiteOverrideReason).HasColumnName("prerequisite_override_reason");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.ApprovedAt).HasColumnName("approved_at");
        builder.Property(e => e.DroppedAt).HasColumnName("dropped_at");
        builder.Property(e => e.DropReason).HasColumnName("drop_reason");

        builder.Property(e => e.Version).HasColumnName("xmin").HasColumnType("xid").IsRowVersion().ValueGeneratedOnAddOrUpdate();

        builder.OwnsOne(e => e.CreditHoursAtEnrollment, credit =>
        {
            credit.Property(v => v.Value).HasColumnName("credit_hours_at_enrollment").IsRequired();
        });
        builder.Navigation(e => e.CreditHoursAtEnrollment).IsRequired();

        ConfigureGrade(builder);
    }

    private static void ConfigureGrade(EntityTypeBuilder<Enrollment> builder)
    {
        builder.OwnsOne(e => e.Grade, grade =>
        {
            grade.ToTable("grades");
            grade.Property(g => g.Id).HasConversion(id => id.Value, value => new GradeId(value)).ValueGeneratedNever().HasColumnName("id");
            grade.Property<EnrollmentId>("EnrollmentId").HasConversion(id => id.Value, value => new EnrollmentId(value)).HasColumnName("enrollment_id");
            grade.WithOwner().HasForeignKey("EnrollmentId");
            grade.HasKey(g => g.Id);
            grade.HasIndex("EnrollmentId").IsUnique().HasDatabaseName("ux_grades_enrollment_id");

            grade.Property(g => g.LetterGrade).HasColumnName("letter_grade");
            grade.Property(g => g.SubmittedAt).HasColumnName("submitted_at");
            grade.Property(g => g.SubmittedByUserId).HasColumnName("submitted_by_user_id");

            grade.OwnsOne(g => g.CalculatedScore, score =>
            {
                score.Property(s => s.Value).HasColumnName("calculated_score_value").HasColumnType("numeric(6,2)");
                score.Property(s => s.Scale).HasColumnName("calculated_score_scale").HasConversion<string>();
            });

            grade.OwnsMany(g => g.Scores, score =>
            {
                score.ToTable("assessment_score_entries");
                score.Property(s => s.Id).ValueGeneratedNever().HasColumnName("id");
                score.Property<GradeId>("GradeId").HasConversion(id => id.Value, value => new GradeId(value)).HasColumnName("grade_id");
                score.WithOwner().HasForeignKey("GradeId");
                score.HasKey(s => s.Id);

                score.Property(s => s.AssessmentId).HasColumnName("assessment_id").IsRequired();
                score.Property(s => s.Score).HasColumnName("score").HasColumnType("numeric(6,2)").IsRequired();
                score.HasIndex("GradeId").HasDatabaseName("ix_assessment_score_entries_grade_id");
            });
            grade.Navigation(g => g.Scores).UsePropertyAccessMode(PropertyAccessMode.Field);

            grade.OwnsMany(g => g.Corrections, correction =>
            {
                correction.ToTable("grade_correction_entries");
                correction.Property(c => c.Id).ValueGeneratedNever().HasColumnName("id");
                correction.Property<GradeId>("GradeId").HasConversion(id => id.Value, value => new GradeId(value)).HasColumnName("grade_id");
                correction.WithOwner().HasForeignKey("GradeId");
                correction.HasKey(c => c.Id);

                correction.Property(c => c.PreviousScore).HasColumnName("previous_score").HasColumnType("numeric(6,2)").IsRequired();
                correction.Property(c => c.NewScore).HasColumnName("new_score").HasColumnType("numeric(6,2)").IsRequired();
                correction.Property(c => c.Reason).HasColumnName("reason").IsRequired();
                correction.Property(c => c.CorrectedByUserId).HasColumnName("corrected_by_user_id").IsRequired();
                correction.Property(c => c.CorrectedAt).HasColumnName("corrected_at").IsRequired();
                correction.HasIndex("GradeId").HasDatabaseName("ix_grade_correction_entries_grade_id");
            });
            grade.Navigation(g => g.Corrections).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Learning.Domain.Assignments;

namespace UMS.Modules.Learning.Infrastructure.Persistence.Configurations;

internal sealed class AssignmentConfiguration : IEntityTypeConfiguration<Assignment>
{
    public void Configure(EntityTypeBuilder<Assignment> builder)
    {
        builder.ToTable("assignments");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasConversion(id => id.Value, value => new AssignmentId(value)).ValueGeneratedNever().HasColumnName("id");

        builder.Property(a => a.CourseOfferingId).HasColumnName("course_offering_id").IsRequired();
        builder.Property(a => a.Title).HasColumnName("title").HasMaxLength(300).IsRequired();
        builder.Property(a => a.Instructions).HasColumnName("instructions").IsRequired();
        builder.Property(a => a.AllowedSubmissionType).HasColumnName("allowed_submission_type").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(a => a.AllowResubmission).HasColumnName("allow_resubmission").IsRequired();
        builder.Property(a => a.MaxPoints).HasColumnName("max_points").IsRequired();
        builder.Property(a => a.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        builder.Property(a => a.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(a => a.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(a => a.PublishedAt).HasColumnName("published_at");
        builder.Property(a => a.ClosedAt).HasColumnName("closed_at");
        builder.Property(a => a.CancelledAt).HasColumnName("cancelled_at");
        builder.Property(a => a.CancellationReason).HasColumnName("cancellation_reason").HasMaxLength(1000);
        builder.Property(a => a.Version).HasColumnName("xmin").HasColumnType("xid").IsRowVersion().ValueGeneratedOnAddOrUpdate();

        builder.HasIndex(a => a.CourseOfferingId).HasDatabaseName("ix_assignments_course_offering_id");

        // LRN-9's hardCloseAt sweep polls on (status, hard_close_at) - indexed so the worker's
        // every-pass query stays a range scan rather than a growing sequential scan.
        builder.HasIndex(a => new { a.Status }).HasDatabaseName("ix_assignments_status");

        builder.OwnsOne(a => a.SubmissionWindow, window =>
        {
            window.Property(w => w.OpensAt).HasColumnName("window_opens_at").IsRequired();
            window.Property(w => w.Deadline).HasColumnName("window_deadline").IsRequired();
            window.Property(w => w.GracePeriod).HasColumnName("window_grace_period").IsRequired();
            window.Property(w => w.HardCloseAt).HasColumnName("window_hard_close_at").IsRequired();
            window.Ignore(w => w.EffectiveDeadline);

            // See LatePenaltyPolicyConverter's own remarks for why the tier schedule is one jsonb
            // column rather than a child table.
            window.Property(w => w.LatePenaltyPolicy)
                .HasColumnName("window_late_penalty_tiers")
                .HasColumnType("jsonb")
                .HasConversion(LatePenaltyPolicyConverter.Converter, LatePenaltyPolicyConverter.Comparer)
                .IsRequired();
        });
        builder.Navigation(a => a.SubmissionWindow).IsRequired();

        builder.OwnsMany(a => a.Extensions, extension =>
        {
            extension.ToTable("submission_extensions");
            extension.Property(e => e.Id).HasConversion(id => id.Value, value => new SubmissionExtensionId(value)).ValueGeneratedNever().HasColumnName("id");
            extension.WithOwner().HasForeignKey(e => e.AssignmentId);
            extension.Property(e => e.AssignmentId).HasConversion(id => id.Value, value => new AssignmentId(value)).HasColumnName("assignment_id");
            extension.HasKey(e => e.Id);

            extension.Property(e => e.StudentId).HasColumnName("student_id").IsRequired();
            extension.Property(e => e.StudentUserId).HasColumnName("student_user_id").IsRequired();
            extension.Property(e => e.ExtendedDeadline).HasColumnName("extended_deadline").IsRequired();
            extension.Property(e => e.GrantedByUserId).HasColumnName("granted_by_user_id").IsRequired();
            extension.Property(e => e.Reason).HasColumnName("reason").HasMaxLength(2000).IsRequired();
            extension.Property(e => e.WaivesLatePenalty).HasColumnName("waives_late_penalty").IsRequired();
            extension.Property(e => e.GrantedAt).HasColumnName("granted_at").IsRequired();

            // requirement-spec.md §4: "A SubmissionExtension is scoped to exactly one (Assignment,
            // Student) pair". Enforced at the database, not only by the aggregate's own re-grant
            // replacement, so no future write path can produce two live extensions for one Student.
            extension.HasIndex(e => new { e.AssignmentId, e.StudentId }).IsUnique().HasDatabaseName("ux_submission_extensions_assignment_student");
        });
        builder.Navigation(a => a.Extensions).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Learning.Domain.Assignments;
using UMS.Modules.Learning.Domain.PlagiarismChecks;
using UMS.Modules.Learning.Domain.Submissions;

namespace UMS.Modules.Learning.Infrastructure.Persistence.Configurations;

internal sealed class SubmissionConfiguration : IEntityTypeConfiguration<Submission>
{
    public void Configure(EntityTypeBuilder<Submission> builder)
    {
        builder.ToTable("submissions");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasConversion(id => id.Value, value => new SubmissionId(value)).ValueGeneratedNever().HasColumnName("id");

        builder.Property(s => s.AssignmentId).HasConversion(id => id.Value, value => new AssignmentId(value)).HasColumnName("assignment_id").IsRequired();
        builder.Property(s => s.CourseOfferingId).HasColumnName("course_offering_id").IsRequired();
        builder.Property(s => s.StudentId).HasColumnName("student_id").IsRequired();
        builder.Property(s => s.SubmittedByUserId).HasColumnName("submitted_by_user_id").IsRequired();
        builder.Property(s => s.TextContent).HasColumnName("text_content");
        builder.Property(s => s.SubmittedAt).HasColumnName("submitted_at").IsRequired();
        builder.Property(s => s.IsLate).HasColumnName("is_late").IsRequired();
        builder.Property(s => s.LatePenaltyPercentage).HasColumnName("late_penalty_percentage").HasPrecision(5, 2).IsRequired();
        builder.Property(s => s.AppliedExtensionId)
            .HasConversion(id => id!.Value.Value, value => new SubmissionExtensionId(value))
            .HasColumnName("applied_extension_id");
        builder.Property(s => s.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(s => s.SupersededBySubmissionId)
            .HasConversion(id => id!.Value.Value, value => new SubmissionId(value))
            .HasColumnName("superseded_by_submission_id");
        builder.Property(s => s.SupersededAt).HasColumnName("superseded_at");
        builder.Property(s => s.EvaluatedByUserId).HasColumnName("evaluated_by_user_id");
        builder.Property(s => s.EvaluatedAt).HasColumnName("evaluated_at");
        builder.Property(s => s.Version).HasColumnName("xmin").HasColumnType("xid").IsRowVersion().ValueGeneratedOnAddOrUpdate();
        builder.Ignore(s => s.LatestPlagiarismCheck);

        builder.HasIndex(s => new { s.AssignmentId, s.StudentId }).HasDatabaseName("ix_submissions_assignment_student");
        builder.HasIndex(s => s.CourseOfferingId).HasDatabaseName("ix_submissions_course_offering_id");

        // Deliberately NOT a unique index on (assignment_id, student_id): the superseded-chain
        // pattern means a Student legitimately accumulates many rows for one Assignment, exactly
        // one of which is non-Superseded at a time (design-decisions.md, "Submission Immutability &
        // Resubmission via Superseded-Chain Pattern"). A partial unique index over the
        // non-superseded rows would express that, but it would also make the resubmission
        // transaction's own supersede-then-insert ordering constraint-sensitive for no correctness
        // gain - the supersession and the insert already happen in one transaction.
        builder.HasIndex(s => new { s.AssignmentId, s.Status }).HasDatabaseName("ix_submissions_assignment_status");

        builder.OwnsOne(s => s.Score, score =>
        {
            score.Property(x => x.RawPoints).HasColumnName("score_raw_points").HasPrecision(9, 2);
            score.Property(x => x.AwardedPoints).HasColumnName("score_awarded_points").HasPrecision(9, 2);
            score.Property(x => x.MaxPoints).HasColumnName("score_max_points");
            score.Property(x => x.AppliedLatePenaltyPercentage).HasColumnName("score_applied_late_penalty_percentage").HasPrecision(5, 2);
            score.Property(x => x.Feedback).HasColumnName("score_feedback");
        });

        builder.OwnsMany(s => s.Files, file =>
        {
            file.ToTable("submission_files");
            file.WithOwner().HasForeignKey("SubmissionId");
            file.Property<SubmissionId>("SubmissionId").HasConversion(id => id.Value, value => new SubmissionId(value)).HasColumnName("submission_id");
            file.Property<int>("Ordinal").HasColumnName("ordinal");
            file.HasKey("SubmissionId", "Ordinal");

            file.Property(f => f.ArtifactId).HasColumnName("artifact_id").IsRequired();
            file.Property(f => f.FileName).HasColumnName("file_name").HasMaxLength(500).IsRequired();
            file.Property(f => f.MimeType).HasColumnName("mime_type").HasMaxLength(200).IsRequired();
        });
        builder.Navigation(s => s.Files).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(s => s.PlagiarismChecks, check =>
        {
            check.ToTable("plagiarism_checks");
            check.Property(c => c.Id).HasConversion(id => id.Value, value => new PlagiarismCheckId(value)).ValueGeneratedNever().HasColumnName("id");
            check.WithOwner().HasForeignKey(c => c.SubmissionId);
            check.Property(c => c.SubmissionId).HasConversion(id => id.Value, value => new SubmissionId(value)).HasColumnName("submission_id");
            check.HasKey(c => c.Id);

            check.Property(c => c.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32).IsRequired();
            check.Property(c => c.AttemptCount).HasColumnName("attempt_count").IsRequired();
            check.Property(c => c.QueuedAt).HasColumnName("queued_at").IsRequired();
            check.Property(c => c.StartedAt).HasColumnName("started_at");
            check.Property(c => c.CompletedAt).HasColumnName("completed_at");
            check.Property(c => c.FailureReason).HasColumnName("failure_reason").HasMaxLength(2000);
            check.Ignore(c => c.IsTerminal);

            check.HasIndex(c => c.Status).HasDatabaseName("ix_plagiarism_checks_status");

            check.OwnsOne(c => c.Score, score =>
            {
                score.Property(x => x.SimilarityPercentage).HasColumnName("score_similarity_percentage").HasPrecision(5, 2);
                score.Property(x => x.MatchedSourceSummary).HasColumnName("score_matched_source_summary");
                score.Property(x => x.ProviderName).HasColumnName("score_provider_name").HasMaxLength(200);
            });
        });
        builder.Navigation(s => s.PlagiarismChecks).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

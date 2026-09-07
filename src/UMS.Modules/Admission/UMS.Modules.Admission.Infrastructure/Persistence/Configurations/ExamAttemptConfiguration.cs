using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Admission.Domain.ExamAttempts;
using UMS.Modules.Admission.Domain.Tests;

namespace UMS.Modules.Admission.Infrastructure.Persistence.Configurations;

internal sealed class ExamAttemptConfiguration : IEntityTypeConfiguration<ExamAttempt>
{
    public void Configure(EntityTypeBuilder<ExamAttempt> builder)
    {
        builder.ToTable("exam_attempts");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasConversion(id => id.Value, value => new ExamAttemptId(value)).ValueGeneratedNever().HasColumnName("id");

        builder.Property(a => a.ApplicantId).HasColumnName("applicant_id").IsRequired();
        builder.Property(a => a.AdmissionTestId).HasColumnName("admission_test_id").IsRequired();
        builder.Property(a => a.TestSlotId).HasColumnName("test_slot_id").IsRequired();
        builder.Property(a => a.RollNumber).HasColumnName("roll_number").HasMaxLength(50).IsRequired();
        builder.Property(a => a.ProctoringSessionId).HasColumnName("proctoring_session_id").HasMaxLength(200);
        builder.Property(a => a.SeedValue).HasColumnName("seed_value").IsRequired();
        builder.Property(a => a.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(a => a.StartedAt).HasColumnName("started_at").IsRequired();
        builder.Property(a => a.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.Property(a => a.SubmittedAt).HasColumnName("submitted_at");
        builder.Property(a => a.SubmissionSource).HasColumnName("submission_source").HasMaxLength(20);
        builder.Property(a => a.EvaluationStatus).HasColumnName("evaluation_status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(a => a.ObjectiveScore).HasColumnName("objective_score").HasPrecision(8, 2);
        builder.Property(a => a.SubjectiveScore).HasColumnName("subjective_score").HasPrecision(8, 2);
        builder.Property(a => a.Version).HasColumnName("xmin").HasColumnType("xid").IsRowVersion().ValueGeneratedOnAddOrUpdate();

        builder.HasIndex(a => new { a.ApplicantId, a.AdmissionTestId }).IsUnique().HasDatabaseName("ux_exam_attempts_applicant_test");
        builder.HasIndex(a => new { a.Status, a.ExpiresAt }).HasDatabaseName("ix_exam_attempts_status_expires_at");

        var selectedQuestionIdsConverter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<IReadOnlyCollection<QuestionId>, string>(
            ids => System.Text.Json.JsonSerializer.Serialize(ids.Select(i => i.Value).ToList(), (System.Text.Json.JsonSerializerOptions?)null),
            json => (System.Text.Json.JsonSerializer.Deserialize<List<Guid>>(json, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<Guid>()).Select(i => new QuestionId(i)).ToList());
        var selectedQuestionIdsComparer = new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<IReadOnlyCollection<QuestionId>>(
            (left, right) => (left ?? new List<QuestionId>()).SequenceEqual(right ?? new List<QuestionId>()),
            list => list.Aggregate(0, (hash, item) => HashCode.Combine(hash, item)),
            list => list.ToList());

        builder.Property(a => a.SelectedQuestionIds)
            .HasConversion(selectedQuestionIdsConverter)
            .Metadata.SetValueComparer(selectedQuestionIdsComparer);
        builder.Property(a => a.SelectedQuestionIds).HasColumnName("selected_question_ids").HasColumnType("jsonb").UsePropertyAccessMode(PropertyAccessMode.Field).IsRequired();

        builder.OwnsMany(a => a.Answers, answer =>
        {
            answer.ToTable("exam_attempt_answers");
            answer.WithOwner().HasForeignKey("ExamAttemptId");
            answer.Property<ExamAttemptId>("ExamAttemptId").HasConversion(id => id.Value, value => new ExamAttemptId(value)).HasColumnName("exam_attempt_id");
            answer.Property(a => a.QuestionId).HasConversion(id => id.Value, value => new QuestionId(value)).HasColumnName("question_id").IsRequired();

            // Keyed by a shadow Ordinal, not (ExamAttemptId, QuestionId) directly - EF's own
            // new-vs-existing heuristic for an owned entity added to an already-tracked, already-
            // persisted parent (SaveAnswerAsync re-loads an existing ExamAttempt, then appends to an
            // initially-empty Answers collection) cannot reliably tell "new" from "existing" when
            // the key is a fully caller-supplied business value - the exact bug this build's own
            // manual verification pass caught for ProgramChoice (ApplicationConfiguration's own
            // remarks). A shadow Ordinal sidesteps it the same way; the real natural-key uniqueness
            // is still enforced via the index below.
            answer.Property<int>("Ordinal").HasColumnName("ordinal");
            answer.HasKey("ExamAttemptId", "Ordinal");
            answer.HasIndex("ExamAttemptId", "QuestionId").IsUnique();

            answer.Property(a => a.SelectedOptionIndex).HasColumnName("selected_option_index");
            answer.Property(a => a.SubjectiveText).HasColumnName("subjective_text");
            answer.Property(a => a.AnsweredAt).HasColumnName("answered_at").IsRequired();
        });
        builder.Navigation(a => a.Answers).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(a => a.IntegrityFlags, flag =>
        {
            flag.ToTable("exam_attempt_integrity_flags");
            flag.WithOwner().HasForeignKey("ExamAttemptId");
            flag.Property<ExamAttemptId>("ExamAttemptId").HasConversion(id => id.Value, value => new ExamAttemptId(value)).HasColumnName("exam_attempt_id");
            flag.HasKey(f => f.Id);

            flag.Property(f => f.Id).ValueGeneratedNever().HasColumnName("id");
            flag.Property(f => f.AnomalyType).HasColumnName("anomaly_type").HasMaxLength(100).IsRequired();
            flag.Property(f => f.Details).HasColumnName("details").HasMaxLength(2000).IsRequired();
            flag.Property(f => f.ConfidenceScore).HasColumnName("confidence_score").HasPrecision(5, 4).IsRequired();
            flag.Property(f => f.RaisedAt).HasColumnName("raised_at").IsRequired();
            flag.Property(f => f.Outcome).HasColumnName("outcome").HasConversion<string>().HasMaxLength(20).IsRequired();
            flag.Property(f => f.ReviewedByUserId).HasColumnName("reviewed_by_user_id");
            flag.Property(f => f.ReviewNotes).HasColumnName("review_notes").HasMaxLength(2000);
            flag.Property(f => f.ReviewedAt).HasColumnName("reviewed_at");
        });
        builder.Navigation(a => a.IntegrityFlags).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

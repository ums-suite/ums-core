using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Admission.Domain.Tests;

namespace UMS.Modules.Admission.Infrastructure.Persistence.Configurations;

internal sealed class AdmissionTestConfiguration : IEntityTypeConfiguration<AdmissionTest>
{
    public void Configure(EntityTypeBuilder<AdmissionTest> builder)
    {
        builder.ToTable("admission_tests");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasConversion(id => id.Value, value => new AdmissionTestId(value)).ValueGeneratedNever().HasColumnName("id");

        builder.Property(t => t.CampaignId).HasColumnName("campaign_id").IsRequired();
        builder.Property(t => t.Name).HasColumnName("name").HasMaxLength(300).IsRequired();
        builder.Property(t => t.DurationMinutes).HasColumnName("duration_minutes").IsRequired();
        builder.Property(t => t.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(t => t.Version).HasColumnName("xmin").HasColumnType("xid").IsRowVersion().ValueGeneratedOnAddOrUpdate();

        builder.HasIndex(t => t.CampaignId).IsUnique().HasDatabaseName("ux_admission_tests_campaign_id");

        builder.OwnsMany(t => t.Questions, question =>
        {
            question.ToTable("admission_test_questions");
            question.WithOwner().HasForeignKey("AdmissionTestId");
            question.Property<AdmissionTestId>("AdmissionTestId").HasConversion(id => id.Value, value => new AdmissionTestId(value)).HasColumnName("admission_test_id");
            question.HasKey(q => q.Id);

            question.Property(q => q.Id).HasConversion(id => id.Value, value => new QuestionId(value)).ValueGeneratedNever().HasColumnName("id");
            question.Property(q => q.Category).HasColumnName("category").HasMaxLength(200).IsRequired();
            question.Property(q => q.Difficulty).HasColumnName("difficulty").HasConversion<string>().HasMaxLength(20).IsRequired();
            question.Property(q => q.Text).HasColumnName("text").IsRequired();
            question.Property(q => q.CorrectOptionIndex).HasColumnName("correct_option_index");
            question.Property(q => q.IsSubjective).HasColumnName("is_subjective").IsRequired();
            question.Property(q => q.MaxScore).HasColumnName("max_score").HasPrecision(6, 2).IsRequired();

            question.Property(q => q.Options)
                .HasConversion(JsonListConverters.StringList)
                .Metadata.SetValueComparer(JsonListConverters.StringListComparer);
            question.Property(q => q.Options).HasColumnName("options").HasColumnType("jsonb").UsePropertyAccessMode(PropertyAccessMode.Field).IsRequired();
        });
        builder.Navigation(t => t.Questions).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(t => t.Slots, slot =>
        {
            slot.ToTable("admission_test_slots");
            slot.WithOwner().HasForeignKey("AdmissionTestId");
            slot.Property<AdmissionTestId>("AdmissionTestId").HasConversion(id => id.Value, value => new AdmissionTestId(value)).HasColumnName("admission_test_id");
            slot.HasKey(s => s.Id);

            slot.Property(s => s.Id).HasConversion(id => id.Value, value => new TestSlotId(value)).ValueGeneratedNever().HasColumnName("id");
            slot.Property(s => s.StartAt).HasColumnName("start_at").IsRequired();
            slot.Property(s => s.EndAt).HasColumnName("end_at").IsRequired();
            slot.Property(s => s.Capacity).HasColumnName("capacity").IsRequired();
            slot.Property(s => s.RemainingSeats).HasColumnName("remaining_seats").IsRequired();
        });
        builder.Navigation(t => t.Slots).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(t => t.SelectionRules, rule =>
        {
            rule.ToTable("admission_test_selection_rules");
            rule.UsePropertyAccessMode(PropertyAccessMode.Field);
            rule.WithOwner().HasForeignKey("AdmissionTestId");
            rule.Property<AdmissionTestId>("AdmissionTestId").HasConversion(id => id.Value, value => new AdmissionTestId(value)).HasColumnName("admission_test_id");
            rule.Property(r => r.Difficulty).HasColumnName("difficulty").HasConversion<string>().HasMaxLength(20).IsRequired();

            // Shadow Ordinal key, not (AdmissionTestId, Difficulty) - see ExamAttemptConfiguration's
            // own remarks on this exact bug class (ProgramChoice); selection rules are added/replaced
            // one-by-one against an already-persisted AdmissionTest the same way.
            rule.Property<int>("Ordinal").HasColumnName("ordinal");
            rule.HasKey("AdmissionTestId", "Ordinal");
            rule.HasIndex("AdmissionTestId", "Difficulty").IsUnique();

            rule.Property(r => r.Count).HasColumnName("count").IsRequired();
        });
        builder.Navigation(t => t.SelectionRules).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

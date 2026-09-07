using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Admission.Domain.Results;

namespace UMS.Modules.Admission.Infrastructure.Persistence.Configurations;

internal sealed class AdmissionResultConfiguration : IEntityTypeConfiguration<AdmissionResult>
{
    public void Configure(EntityTypeBuilder<AdmissionResult> builder)
    {
        builder.ToTable("admission_results");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasConversion(id => id.Value, value => new AdmissionResultId(value)).ValueGeneratedNever().HasColumnName("id");

        builder.Property(r => r.CampaignId).HasColumnName("campaign_id").IsRequired();
        builder.Property(r => r.MeritListId).HasColumnName("merit_list_id").IsRequired();
        builder.Property(r => r.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(r => r.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(r => r.CalculatedAt).HasColumnName("calculated_at");
        builder.Property(r => r.LockedAt).HasColumnName("locked_at");
        builder.Property(r => r.LockedByUserId).HasColumnName("locked_by_user_id");
        builder.Property(r => r.ApprovedAt).HasColumnName("approved_at");
        builder.Property(r => r.ApprovedByUserId).HasColumnName("approved_by_user_id");
        builder.Property(r => r.PublishingStartedAt).HasColumnName("publishing_started_at");
        builder.Property(r => r.PublishedAt).HasColumnName("published_at");
        builder.Property(r => r.PublishedByUserId).HasColumnName("published_by_user_id");
        builder.Property(r => r.ArchivedAt).HasColumnName("archived_at");
        builder.Property(r => r.CorrectionCount).HasColumnName("correction_count").IsRequired();
        builder.Property(r => r.Version).HasColumnName("xmin").HasColumnType("xid").IsRowVersion().ValueGeneratedOnAddOrUpdate();

        builder.HasIndex(r => r.CampaignId).IsUnique().HasDatabaseName("ux_admission_results_campaign_id");

        builder.OwnsMany(r => r.Entries, entry =>
        {
            entry.ToTable("admission_result_entries");
            entry.WithOwner().HasForeignKey("AdmissionResultId");
            entry.Property<AdmissionResultId>("AdmissionResultId").HasConversion(id => id.Value, value => new AdmissionResultId(value)).HasColumnName("admission_result_id");
            entry.HasKey("AdmissionResultId", "ApplicantId");

            entry.Property(e => e.ApplicantId).HasColumnName("applicant_id").IsRequired();
            entry.Property(e => e.ApplicationId).HasColumnName("application_id").IsRequired();
            entry.Property(e => e.ProgramId).HasColumnName("program_id").IsRequired();
            entry.Property(e => e.Outcome).HasColumnName("outcome").HasConversion<string>().HasMaxLength(20).IsRequired();
            entry.Property(e => e.MeritRank).HasColumnName("merit_rank").IsRequired();
            entry.Property(e => e.WaitlistRank).HasColumnName("waitlist_rank");
        });
        builder.Navigation(r => r.Entries).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Admission.Domain.Campaigns;

namespace UMS.Modules.Admission.Infrastructure.Persistence.Configurations;

internal sealed class AdmissionCampaignConfiguration : IEntityTypeConfiguration<AdmissionCampaign>
{
    public void Configure(EntityTypeBuilder<AdmissionCampaign> builder)
    {
        builder.ToTable("admission_campaigns");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasConversion(id => id.Value, value => new AdmissionCampaignId(value)).ValueGeneratedNever().HasColumnName("id");

        builder.Property(c => c.Name).HasColumnName("name").HasMaxLength(300).IsRequired();
        builder.Property(c => c.ApplicationFeeType).HasColumnName("application_fee_type").HasMaxLength(100).IsRequired();
        builder.Property(c => c.ConfirmationFeeType).HasColumnName("confirmation_fee_type").HasMaxLength(100).IsRequired();
        builder.Property(c => c.IsConfigurationLocked).HasColumnName("is_configuration_locked").IsRequired();
        builder.Property(c => c.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(c => c.Version).HasColumnName("xmin").HasColumnType("xid").IsRowVersion().ValueGeneratedOnAddOrUpdate();

        builder.Property(c => c.ProgramIds)
            .HasConversion(JsonListConverters.GuidList)
            .Metadata.SetValueComparer(JsonListConverters.GuidListComparer);
        builder.Property(c => c.ProgramIds).HasColumnName("program_ids").HasColumnType("jsonb").UsePropertyAccessMode(PropertyAccessMode.Field).IsRequired();

        builder.Property(c => c.RequiredDocumentTypes)
            .HasConversion(JsonListConverters.StringList)
            .Metadata.SetValueComparer(JsonListConverters.StringListComparer);
        builder.Property(c => c.RequiredDocumentTypes).HasColumnName("required_document_types").HasColumnType("jsonb").UsePropertyAccessMode(PropertyAccessMode.Field).IsRequired();

        builder.OwnsOne(c => c.ApplicationWindow, window =>
        {
            window.Property(w => w.Start).HasColumnName("application_window_start").IsRequired();
            window.Property(w => w.End).HasColumnName("application_window_end").IsRequired();
        });

        builder.OwnsMany(c => c.EligibilityRules, rule =>
        {
            rule.ToTable("campaign_eligibility_rules");
            rule.UsePropertyAccessMode(PropertyAccessMode.Field);
            rule.WithOwner().HasForeignKey("CampaignId");
            rule.Property<AdmissionCampaignId>("CampaignId").HasConversion(id => id.Value, value => new AdmissionCampaignId(value)).HasColumnName("campaign_id");
            rule.Property<int>("Ordinal").HasColumnName("ordinal");
            rule.HasKey("CampaignId", "Ordinal");

            rule.Property(r => r.ProgramId).HasColumnName("program_id").IsRequired();
            rule.Property(r => r.RequiredBoard).HasColumnName("required_board").HasMaxLength(200);
            rule.OwnsOne(r => r.MinimumScore, score =>
            {
                score.UsePropertyAccessMode(PropertyAccessMode.Field);
                score.Property(s => s.Value).HasColumnName("min_score").HasPrecision(6, 2).IsRequired();
                score.Property(s => s.Scale).HasColumnName("min_score_scale").HasConversion<string>().HasMaxLength(20).IsRequired();
            });
        });
        builder.Navigation(c => c.EligibilityRules).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(c => c.SeatQuotas, quota =>
        {
            quota.ToTable("campaign_seat_quotas");
            quota.UsePropertyAccessMode(PropertyAccessMode.Field);
            quota.WithOwner().HasForeignKey("CampaignId");
            quota.Property<AdmissionCampaignId>("CampaignId").HasConversion(id => id.Value, value => new AdmissionCampaignId(value)).HasColumnName("campaign_id");

            // Shadow Ordinal key, not (CampaignId, ProgramId) - see ExamAttemptConfiguration's own
            // remarks on this exact bug class (ProgramChoice); SeatQuotas are added one-by-one to an
            // already-persisted Campaign the same way.
            quota.Property<int>("Ordinal").HasColumnName("ordinal");
            quota.HasKey("CampaignId", "Ordinal");
            quota.HasIndex("CampaignId", "ProgramId").IsUnique();

            quota.Property(q => q.ProgramId).HasColumnName("program_id").IsRequired();
            quota.Property(q => q.Quota).HasColumnName("quota").IsRequired();
        });
        builder.Navigation(c => c.SeatQuotas).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

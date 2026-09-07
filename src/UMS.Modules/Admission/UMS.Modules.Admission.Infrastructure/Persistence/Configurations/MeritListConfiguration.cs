using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Admission.Domain.MeritLists;

namespace UMS.Modules.Admission.Infrastructure.Persistence.Configurations;

internal sealed class MeritListConfiguration : IEntityTypeConfiguration<MeritList>
{
    public void Configure(EntityTypeBuilder<MeritList> builder)
    {
        builder.ToTable("merit_lists");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasConversion(id => id.Value, value => new MeritListId(value)).ValueGeneratedNever().HasColumnName("id");

        builder.Property(m => m.CampaignId).HasColumnName("campaign_id").IsRequired();
        builder.Property(m => m.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(m => m.GeneratedAt).HasColumnName("generated_at").IsRequired();
        builder.Property(m => m.ApprovedAt).HasColumnName("approved_at");
        builder.Property(m => m.ApprovedByUserId).HasColumnName("approved_by_user_id");
        builder.Property(m => m.Version).HasColumnName("xmin").HasColumnType("xid").IsRowVersion().ValueGeneratedOnAddOrUpdate();

        builder.HasIndex(m => m.CampaignId).IsUnique().HasDatabaseName("ux_merit_lists_campaign_id");

        builder.OwnsMany(m => m.Entries, entry =>
        {
            entry.ToTable("merit_list_entries");
            entry.WithOwner().HasForeignKey("MeritListId");
            entry.Property<MeritListId>("MeritListId").HasConversion(id => id.Value, value => new MeritListId(value)).HasColumnName("merit_list_id");
            entry.HasKey("MeritListId", "ApplicantId");

            entry.Property(e => e.ApplicantId).HasColumnName("applicant_id").IsRequired();
            entry.Property(e => e.ApplicationId).HasColumnName("application_id").IsRequired();
            entry.Property(e => e.ProgramId).HasColumnName("program_id").IsRequired();
            entry.Property(e => e.Score).HasColumnName("score").HasPrecision(8, 2).IsRequired();
            entry.Property(e => e.Rank).HasColumnName("rank").IsRequired();
            entry.Property(e => e.Outcome).HasColumnName("outcome").HasConversion<string>().HasMaxLength(20).IsRequired();
            entry.Property(e => e.WaitlistRank).HasColumnName("waitlist_rank");
        });
        builder.Navigation(m => m.Entries).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

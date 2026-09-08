using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Alumni.Domain.Donations;

namespace UMS.Modules.Alumni.Infrastructure.Persistence.Configurations;

internal sealed class DonationCampaignConfiguration : IEntityTypeConfiguration<DonationCampaign>
{
    public void Configure(EntityTypeBuilder<DonationCampaign> builder)
    {
        builder.ToTable("donation_campaigns");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasConversion(id => id.Value, value => new DonationCampaignId(value)).ValueGeneratedNever().HasColumnName("id");

        builder.Property(c => c.Name).HasColumnName("name").HasMaxLength(300).IsRequired();
        builder.Property(c => c.Description).HasColumnName("description").HasMaxLength(4000);
        builder.Property(c => c.GoalAmount).HasColumnName("goal_amount").HasPrecision(14, 2).IsRequired();
        builder.Property(c => c.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
        builder.Property(c => c.StartsAt).HasColumnName("starts_at").IsRequired();
        builder.Property(c => c.EndsAt).HasColumnName("ends_at").IsRequired();
        builder.Property(c => c.ClosedEarly).HasColumnName("closed_early").IsRequired();
        builder.Property(c => c.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(c => c.Version).HasColumnName("xmin").HasColumnType("xid").IsRowVersion().ValueGeneratedOnAddOrUpdate();

        builder.HasIndex(c => new { c.StartsAt, c.EndsAt }).HasDatabaseName("ix_donation_campaigns_window");
    }
}

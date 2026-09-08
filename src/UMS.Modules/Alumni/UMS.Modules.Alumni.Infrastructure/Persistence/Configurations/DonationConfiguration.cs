using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Alumni.Domain.Donations;

namespace UMS.Modules.Alumni.Infrastructure.Persistence.Configurations;

internal sealed class DonationConfiguration : IEntityTypeConfiguration<Donation>
{
    public void Configure(EntityTypeBuilder<Donation> builder)
    {
        builder.ToTable("donations");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasConversion(id => id.Value, value => new DonationId(value)).ValueGeneratedNever().HasColumnName("id");

        builder.Property(d => d.AlumnusId).HasColumnName("alumnus_id").IsRequired();
        builder.Property(d => d.OwnerUserId).HasColumnName("owner_user_id").IsRequired();
        builder.Property(d => d.CampaignId).HasColumnName("campaign_id").IsRequired();
        builder.Property(d => d.Amount).HasColumnName("amount").HasPrecision(14, 2).IsRequired();
        builder.Property(d => d.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
        builder.Property(d => d.IsAnonymous).HasColumnName("is_anonymous").IsRequired();
        builder.Property(d => d.RecurrenceInterval).HasColumnName("recurrence_interval").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(d => d.RecurrenceStatus).HasColumnName("recurrence_status").HasConversion<string?>(v => v == null ? null : v.ToString(), v => v == null ? null : Enum.Parse<RecurrenceStatus>(v)).HasMaxLength(20);
        builder.Property(d => d.SeriesRootDonationId).HasConversion(id => id == null ? (Guid?)null : id.Value.Value, value => value == null ? (DonationId?)null : new DonationId(value.Value)).HasColumnName("series_root_donation_id");
        builder.Property(d => d.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(d => d.InvoiceId).HasColumnName("invoice_id");
        builder.Property(d => d.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(d => d.ConfirmedAt).HasColumnName("confirmed_at");
        builder.Property(d => d.NextChargeAt).HasColumnName("next_charge_at");
        builder.Property(d => d.Version).HasColumnName("xmin").HasColumnType("xid").IsRowVersion().ValueGeneratedOnAddOrUpdate();

        builder.HasIndex(d => d.AlumnusId).HasDatabaseName("ix_donations_alumnus_id");
        builder.HasIndex(d => d.CampaignId).HasDatabaseName("ix_donations_campaign_id");
        builder.HasIndex(d => d.InvoiceId).HasDatabaseName("ix_donations_invoice_id");
        builder.HasIndex(d => d.SeriesRootDonationId).HasDatabaseName("ix_donations_series_root_donation_id");
        builder.HasIndex(d => new { d.RecurrenceStatus, d.NextChargeAt }).HasDatabaseName("ix_donations_recurrence_status_next_charge_at");
    }
}

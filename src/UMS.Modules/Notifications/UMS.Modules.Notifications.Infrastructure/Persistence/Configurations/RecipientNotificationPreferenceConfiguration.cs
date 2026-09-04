using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Notifications.Domain.Preferences;

namespace UMS.Modules.Notifications.Infrastructure.Persistence.Configurations;

/// <summary>NTF-5's category-level opt-out storage.</summary>
internal sealed class RecipientNotificationPreferenceConfiguration : IEntityTypeConfiguration<RecipientNotificationPreference>
{
    public void Configure(EntityTypeBuilder<RecipientNotificationPreference> builder)
    {
        builder.ToTable("recipient_notification_preferences");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasConversion(id => id.Value, value => new RecipientNotificationPreferenceId(value))
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(p => p.RecipientId).HasColumnName("recipient_id").IsRequired();
        builder.Property(p => p.Category).HasColumnName("category").IsRequired();
        builder.Property(p => p.OptedOut).HasColumnName("opted_out").IsRequired();
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(p => new { p.RecipientId, p.Category }).IsUnique().HasDatabaseName("ux_recipient_notification_preferences_recipient_category");
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Notifications.Domain.Preferences;

namespace UMS.Modules.Notifications.Infrastructure.Persistence.Configurations;

/// <summary>NTF-15's hard-bounce suppression storage.</summary>
internal sealed class ChannelSuppressionConfiguration : IEntityTypeConfiguration<ChannelSuppression>
{
    public void Configure(EntityTypeBuilder<ChannelSuppression> builder)
    {
        builder.ToTable("channel_suppressions");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasConversion(id => id.Value, value => new ChannelSuppressionId(value))
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(s => s.RecipientId).HasColumnName("recipient_id").IsRequired();
        builder.Property(s => s.Channel).HasColumnName("channel").IsRequired();
        builder.Property(s => s.Reason).HasColumnName("reason").IsRequired();
        builder.Property(s => s.SuppressedAt).HasColumnName("suppressed_at").IsRequired();

        builder.HasIndex(s => new { s.RecipientId, s.Channel }).IsUnique().HasDatabaseName("ux_channel_suppressions_recipient_channel");
    }
}

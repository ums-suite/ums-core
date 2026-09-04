using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Notifications.Domain.Requests;

namespace UMS.Modules.Notifications.Infrastructure.Persistence.Configurations;

/// <summary>
/// NTF-1/NTF-4: the dedupe unique constraint (design-decisions.md "Dedup-Key Enforcement Mechanism")
/// lives here, at the database level, named <c>ux_notification_requests_dedupe</c> - the exact name
/// <see cref="NotificationsDbContext"/>'s own <c>SaveChangesAsync</c> override matches against to
/// translate a violation into <see cref="Application.Requests.DuplicateNotificationRequestException"/>.
/// </summary>
internal sealed class NotificationRequestConfiguration : IEntityTypeConfiguration<NotificationRequest>
{
    public void Configure(EntityTypeBuilder<NotificationRequest> builder)
    {
        builder.ToTable("notification_requests");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasConversion(id => id.Value, value => new NotificationRequestId(value))
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(r => r.SourceModule).HasColumnName("source_module").HasMaxLength(100).IsRequired();
        builder.Property(r => r.EventType).HasColumnName("event_type").HasMaxLength(200).IsRequired();
        builder.Property(r => r.SourceEntityId).HasColumnName("source_entity_id").HasMaxLength(200).IsRequired();
        builder.Property(r => r.RecipientId).HasColumnName("recipient_id").IsRequired();
        builder.Property(r => r.Category).HasColumnName("category").IsRequired();
        builder.Property(r => r.Priority).HasColumnName("priority").IsRequired();
        builder.Property(r => r.LanguageOverride).HasColumnName("language_override").HasMaxLength(10);
        builder.Property(r => r.PayloadJson).HasColumnName("payload_json").HasColumnType("jsonb").IsRequired();
        builder.Property(r => r.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(r => new { r.SourceModule, r.EventType, r.SourceEntityId, r.RecipientId })
            .IsUnique()
            .HasDatabaseName("ux_notification_requests_dedupe");

        builder.HasIndex(r => r.RecipientId).HasDatabaseName("ix_notification_requests_recipient");

        builder.HasMany(r => r.Attempts)
            .WithOne()
            .HasForeignKey(a => a.NotificationRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(r => r.Attempts)
            .HasField("_attempts")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

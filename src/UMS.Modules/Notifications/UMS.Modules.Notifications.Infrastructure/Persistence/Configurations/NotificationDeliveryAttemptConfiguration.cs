using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Notifications.Domain.Requests;

namespace UMS.Modules.Notifications.Infrastructure.Persistence.Configurations;

/// <summary>NTF-6/NTF-13: one row per channel per request, its own independent retry/dead-letter state (design-decisions.md "Fan-Out Isolation").</summary>
internal sealed class NotificationDeliveryAttemptConfiguration : IEntityTypeConfiguration<NotificationDeliveryAttempt>
{
    public void Configure(EntityTypeBuilder<NotificationDeliveryAttempt> builder)
    {
        builder.ToTable("notification_delivery_attempts");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasConversion(id => id.Value, value => new NotificationDeliveryAttemptId(value))
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(a => a.NotificationRequestId)
            .HasConversion(id => id.Value, value => new NotificationRequestId(value))
            .HasColumnName("notification_request_id")
            .IsRequired();

        builder.Property(a => a.Channel).HasColumnName("channel").IsRequired();
        builder.Property(a => a.Status).HasColumnName("status").IsRequired();
        builder.Property(a => a.AttemptCount).HasColumnName("attempt_count").IsRequired();
        builder.Property(a => a.NextAttemptAt).HasColumnName("next_attempt_at");
        builder.Property(a => a.InFlightSince).HasColumnName("in_flight_since");
        builder.Property(a => a.DeliveredAt).HasColumnName("delivered_at");
        builder.Property(a => a.ReadAt).HasColumnName("read_at");
        builder.Property(a => a.ProviderMessageId).HasColumnName("provider_message_id").HasMaxLength(200);
        builder.Property(a => a.LastError).HasColumnName("last_error");
        builder.Property(a => a.DeadLetterReason).HasColumnName("dead_letter_reason");
        builder.Property(a => a.RenderedSubject).HasColumnName("rendered_subject");
        builder.Property(a => a.RenderedBody).HasColumnName("rendered_body");
        builder.Property(a => a.RenderedDeepLink).HasColumnName("rendered_deep_link");
        builder.Property(a => a.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(a => a.NotificationRequestId).HasDatabaseName("ix_notification_delivery_attempts_request");

        // NTF-13's claim query shape: WHERE channel = ... AND status IN (...) ORDER BY ...
        builder.HasIndex(a => new { a.Channel, a.Status, a.NextAttemptAt }).HasDatabaseName("ix_notification_delivery_attempts_dispatch");

        // NTF-15's webhook correlation lookup.
        builder.HasIndex(a => new { a.Channel, a.ProviderMessageId }).HasDatabaseName("ix_notification_delivery_attempts_provider_message");

        // NTF-14's dead-letter triage queue.
        builder.HasIndex(a => a.Status).HasDatabaseName("ix_notification_delivery_attempts_status");
    }
}

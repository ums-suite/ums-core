using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Shared.Outbox;

namespace UMS.Modules.Reporting.Infrastructure.Persistence.Configurations;

/// <summary>Reporting's own outbox (ADR-0014) - drained by <c>UMS.Workers</c>' Reporting relays through this module's own <c>IOutboxReader</c>. Mirrors every other module's own configuration exactly.</summary>
internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever().HasColumnName("id");

        builder.Property(m => m.EventType).HasColumnName("event_type").IsRequired();
        builder.Property(m => m.PayloadJson).HasColumnName("payload_json").IsRequired();
        builder.Property(m => m.OccurredAt).HasColumnName("occurred_at").IsRequired();
        builder.Property(m => m.RecordedAt).HasColumnName("recorded_at").IsRequired();
        builder.Property(m => m.ProcessedAt).HasColumnName("processed_at");
        builder.Property(m => m.AttemptCount).HasColumnName("attempt_count").IsRequired();
        builder.Property(m => m.LastError).HasColumnName("last_error");

        builder.HasIndex(m => m.ProcessedAt).HasDatabaseName("ix_outbox_messages_processed_at");
        builder.HasIndex(m => m.EventType).HasDatabaseName("ix_outbox_messages_event_type");
    }
}

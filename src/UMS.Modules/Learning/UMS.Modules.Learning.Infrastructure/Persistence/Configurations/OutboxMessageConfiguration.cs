using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Shared.Outbox;

namespace UMS.Modules.Learning.Infrastructure.Persistence.Configurations;

/// <summary>
/// Learning's own outbox (ADR-0014) - drained by <c>UMS.Workers</c>' Learning relays through this
/// module's own <c>IOutboxReader</c>, never by a raw cross-module SQL read (unlike Academic's own
/// outbox table, which Faculty's FAC-4 projection relay genuinely does read by name). Explicit
/// snake_case column names all the same, matching Academic's own configuration rather than leaving
/// EF's default PascalCase.
/// </summary>
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

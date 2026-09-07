using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Shared.Outbox;

namespace UMS.Modules.Academic.Infrastructure.Persistence.Configurations;

/// <summary>
/// Deliberately explicit, unlike Faculty/Student's own outbox table (which is left at EF's default
/// PascalCase naming since nothing outside those modules reads their outbox table by raw SQL) -
/// this module's outbox table IS read directly, by name, from outside this module:
/// <c>UMS.Modules.Faculty.Infrastructure.CrossModule.AcademicOutboxEventSource</c> (FAC-4, already
/// merged) queries <c>academic.outbox_messages</c>'s columns (<c>id, event_type, payload_json,
/// occurred_at</c>) directly via a plain Npgsql connection. Table/column names here are therefore a
/// load-bearing cross-module contract, not just a style choice - changing any of them is a breaking
/// change to that consumer.
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

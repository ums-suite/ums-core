using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Shared.Outbox;

namespace UMS.Modules.Audit.Infrastructure.Persistence.Configurations;

/// <summary>
/// Audit's own <c>audit.outbox_messages</c> table (ADR-0014) - the same shared row shape Identity
/// uses for its own <c>identity.outbox_messages</c> table, per-module data, shared C# type only
/// (see <c>UMS.Shared.Outbox.OutboxMessage</c>'s remarks). Feeds AUD-9's export relay in
/// <c>UMS.Workers</c>.
/// </summary>
internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(m => m.EventType).HasColumnName("event_type").IsRequired();
        builder.Property(m => m.PayloadJson).HasColumnName("payload_json").HasColumnType("jsonb").IsRequired();
        builder.Property(m => m.OccurredAt).HasColumnName("occurred_at").IsRequired();
        builder.Property(m => m.RecordedAt).HasColumnName("recorded_at").IsRequired();
        builder.Property(m => m.ProcessedAt).HasColumnName("processed_at");
        builder.Property(m => m.AttemptCount).HasColumnName("attempt_count").IsRequired();
        builder.Property(m => m.LastError).HasColumnName("last_error");

        builder.HasIndex(m => m.ProcessedAt).HasDatabaseName("ix_audit_outbox_messages_processed_at");
    }
}

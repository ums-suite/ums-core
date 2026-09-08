using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Alumni.Infrastructure.Persistence.Inbox;

namespace UMS.Modules.Alumni.Infrastructure.Persistence.Configurations;

internal sealed class ProcessedInboundEventConfiguration : IEntityTypeConfiguration<ProcessedInboundEvent>
{
    public void Configure(EntityTypeBuilder<ProcessedInboundEvent> builder)
    {
        builder.ToTable("processed_inbound_events");
        builder.HasKey(e => new { e.EventId, e.Source });
        builder.Property(e => e.EventId).HasColumnName("event_id");
        builder.Property(e => e.Source).HasColumnName("source").HasMaxLength(50);
        builder.Property(e => e.ProcessedAt).HasColumnName("processed_at").IsRequired();
    }
}

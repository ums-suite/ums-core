using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Research.Infrastructure.Persistence.Inbox;

namespace UMS.Modules.Research.Infrastructure.Persistence.Configurations;

internal sealed class ProcessedInboundEventConfiguration : IEntityTypeConfiguration<ProcessedInboundEvent>
{
    public void Configure(EntityTypeBuilder<ProcessedInboundEvent> builder)
    {
        builder.ToTable("processed_inbound_events");
        builder.Property(e => e.EventId).ValueGeneratedNever().HasColumnName("event_id");
        builder.Property(e => e.Source).HasColumnName("source").HasMaxLength(50).IsRequired();
        builder.Property(e => e.ProcessedAt).HasColumnName("processed_at").IsRequired();

        builder.HasKey(e => new { e.EventId, e.Source });
    }
}

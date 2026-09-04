using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace UMS.Modules.Faculty.Infrastructure.Persistence.Configurations;

internal sealed class ProcessedInboundEventConfiguration : IEntityTypeConfiguration<ProcessedInboundEvent>
{
    public void Configure(EntityTypeBuilder<ProcessedInboundEvent> builder)
    {
        builder.ToTable("processed_inbound_events");
        builder.HasKey(e => e.EventId);
        builder.Property(e => e.EventId).HasColumnName("event_id").ValueGeneratedNever();
        builder.Property(e => e.ProcessedAt).HasColumnName("processed_at").IsRequired();
    }
}

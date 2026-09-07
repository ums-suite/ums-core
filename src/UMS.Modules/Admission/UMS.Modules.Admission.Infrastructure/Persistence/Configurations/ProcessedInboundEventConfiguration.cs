using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Admission.Infrastructure.Persistence.Inbox;

namespace UMS.Modules.Admission.Infrastructure.Persistence.Configurations;

internal sealed class ProcessedInboundEventConfiguration : IEntityTypeConfiguration<ProcessedInboundEvent>
{
    public void Configure(EntityTypeBuilder<ProcessedInboundEvent> builder)
    {
        builder.ToTable("processed_inbound_events");
        builder.HasKey(e => e.EventId);
        builder.Property(e => e.EventId).ValueGeneratedNever().HasColumnName("event_id");
        builder.Property(e => e.ProcessedAt).HasColumnName("processed_at").IsRequired();
    }
}

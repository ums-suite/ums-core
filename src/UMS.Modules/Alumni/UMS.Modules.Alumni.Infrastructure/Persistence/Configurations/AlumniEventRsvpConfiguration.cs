using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Alumni.Domain.AlumniEvents;

namespace UMS.Modules.Alumni.Infrastructure.Persistence.Configurations;

internal sealed class AlumniEventRsvpConfiguration : IEntityTypeConfiguration<AlumniEventRsvp>
{
    public void Configure(EntityTypeBuilder<AlumniEventRsvp> builder)
    {
        builder.ToTable("alumni_event_rsvps");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(r => r.EventId).HasConversion(id => id.Value, value => new AlumniEventId(value)).HasColumnName("event_id").IsRequired();
        builder.Property(r => r.AlumnusId).HasColumnName("alumnus_id").IsRequired();
        builder.Property(r => r.Response).HasColumnName("response").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(r => r.GuestCount).HasColumnName("guest_count").IsRequired();
        builder.Property(r => r.RespondedAt).HasColumnName("responded_at").IsRequired();

        builder.HasIndex(r => new { r.EventId, r.AlumnusId }).IsUnique().HasDatabaseName("ux_alumni_event_rsvps_event_alumnus");
    }
}

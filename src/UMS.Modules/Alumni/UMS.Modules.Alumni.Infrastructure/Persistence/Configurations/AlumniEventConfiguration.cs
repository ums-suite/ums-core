using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Alumni.Domain.AlumniEvents;

namespace UMS.Modules.Alumni.Infrastructure.Persistence.Configurations;

internal sealed class AlumniEventConfiguration : IEntityTypeConfiguration<AlumniEvent>
{
    public void Configure(EntityTypeBuilder<AlumniEvent> builder)
    {
        builder.ToTable("alumni_events");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasConversion(id => id.Value, value => new AlumniEventId(value)).ValueGeneratedNever().HasColumnName("id");

        builder.Property(e => e.Title).HasColumnName("title").HasMaxLength(300).IsRequired();
        builder.Property(e => e.Description).HasColumnName("description").HasMaxLength(4000);
        builder.Property(e => e.ChapterId).HasColumnName("chapter_id");
        builder.Property(e => e.ContentEventId).HasColumnName("content_event_id");
        builder.Property(e => e.EventDate).HasColumnName("event_date").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.Version).HasColumnName("xmin").HasColumnType("xid").IsRowVersion().ValueGeneratedOnAddOrUpdate();

        builder.HasIndex(e => e.ChapterId).HasDatabaseName("ix_alumni_events_chapter_id");
        builder.HasIndex(e => e.EventDate).HasDatabaseName("ix_alumni_events_event_date");
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Content.Domain.Events;

namespace UMS.Modules.Content.Infrastructure.Persistence.Configurations;

/// <summary>CNT-7: `events` + `event_translations` (ADR-0011).</summary>
internal sealed class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.ToTable("events");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasConversion(id => id.Value, value => new EventId(value))
            .ValueGeneratedNever()
            .HasColumnName("id");

        builder.Property(e => e.Title).HasColumnName("title").IsRequired();
        builder.Property(e => e.Body).HasColumnName("body").IsRequired();
        builder.Property(e => e.LocationLabel).HasColumnName("location_label");
        builder.Property(e => e.Audience).HasColumnName("audience").HasConversion<int>().IsRequired();
        builder.Property(e => e.OrganizationNodeId).HasColumnName("organization_node_id");
        builder.Property(e => e.StartAt).HasColumnName("start_at").IsRequired();
        builder.Property(e => e.EndAt).HasColumnName("end_at").IsRequired();
        builder.Property(e => e.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        builder.Property(e => e.UpdatedByUserId).HasColumnName("updated_by_user_id").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.Property(e => e.Version)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsRowVersion()
            .ValueGeneratedOnAddOrUpdate();

        // CNT-7: calendar filtering by date range/audience/Organization node.
        builder.HasIndex(e => new { e.StartAt, e.EndAt }).HasDatabaseName("ix_events_start_end");
        builder.HasIndex(e => e.Audience).HasDatabaseName("ix_events_audience");

        // Same shadow-Ordinal-key gotcha as NoticeTranslation - see NoticeConfiguration's own remarks.
        builder.OwnsMany(e => e.Translations, translation =>
        {
            translation.ToTable("event_translations");
            translation.UsePropertyAccessMode(PropertyAccessMode.Field);
            translation.WithOwner().HasForeignKey("EventId");
            translation.Property<EventId>("EventId").HasConversion(id => id.Value, value => new EventId(value)).HasColumnName("event_id");
            translation.Property<int>("Ordinal").HasColumnName("ordinal");
            translation.HasKey("EventId", "Ordinal");
            translation.HasIndex("EventId", "LanguageCode").IsUnique().HasDatabaseName("ux_event_translations_event_language");

            translation.Property(t => t.LanguageCode).HasColumnName("language_code").HasMaxLength(10).IsRequired();
            translation.Property(t => t.Title).HasColumnName("title").IsRequired();
            translation.Property(t => t.Body).HasColumnName("body").IsRequired();
            translation.Property(t => t.LocationLabel).HasColumnName("location_label");
        });
        builder.Navigation(e => e.Translations).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

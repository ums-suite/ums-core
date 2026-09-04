using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Notifications.Domain.Templates;

namespace UMS.Modules.Notifications.Infrastructure.Persistence.Configurations;

/// <summary>NTF-7: <c>templates</c>/<c>template_translations</c> - ums-conventions.md's Localization Implementation pattern, "a <c>{table}_translations</c> table keyed <c>(entity_id, language_code)</c>".</summary>
internal sealed class TemplateConfiguration : IEntityTypeConfiguration<Template>
{
    public void Configure(EntityTypeBuilder<Template> builder)
    {
        builder.ToTable("templates");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasConversion(id => id.Value, value => new TemplateId(value))
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(t => t.EventType).HasColumnName("event_type").HasMaxLength(200).IsRequired();
        builder.Property(t => t.Channel).HasColumnName("channel").IsRequired();
        builder.Property(t => t.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(t => t.Version).HasColumnName("version").IsRequired();
        builder.Property(t => t.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(t => new { t.EventType, t.Channel }).IsUnique().HasDatabaseName("ux_templates_event_type_channel");

        builder.Navigation(t => t.Translations)
            .HasField("_translations")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(t => t.Translations, translation =>
        {
            translation.ToTable("template_translations");
            translation.WithOwner().HasForeignKey("template_id");
            translation.Property<Guid>("id").ValueGeneratedOnAdd();
            translation.HasKey("id");

            translation.Property(tt => tt.LanguageCode).HasColumnName("language_code").HasMaxLength(10).IsRequired();
            translation.Property(tt => tt.Subject).HasColumnName("subject").HasMaxLength(500);
            translation.Property(tt => tt.Body).HasColumnName("body").IsRequired();
            translation.Property(tt => tt.PushTitle).HasColumnName("push_title").HasMaxLength(200);
            translation.Property(tt => tt.DeepLink).HasColumnName("deep_link").HasMaxLength(500);
            translation.Property(tt => tt.UpdatedAt).HasColumnName("updated_at").IsRequired();

            translation.HasIndex("template_id", nameof(TemplateTranslation.LanguageCode))
                .IsUnique()
                .HasDatabaseName("ux_template_translations_template_language");
        });
    }
}

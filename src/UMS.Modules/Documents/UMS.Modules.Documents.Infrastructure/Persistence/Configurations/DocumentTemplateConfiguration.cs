using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Documents.Domain.Templates;

namespace UMS.Modules.Documents.Infrastructure.Persistence.Configurations;

/// <summary>DOC-1: <see cref="DocumentTemplate"/> is versioned by inserting a new row per publish - never updated in place (see the aggregate's own remarks) - so <c>(document_type, version)</c> is a real database unique constraint, not just an application-level convention.</summary>
internal sealed class DocumentTemplateConfiguration : IEntityTypeConfiguration<DocumentTemplate>
{
    public void Configure(EntityTypeBuilder<DocumentTemplate> builder)
    {
        builder.ToTable("document_templates");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasConversion(id => id.Value, value => new DocumentTemplateId(value))
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(t => t.DocumentType).HasColumnName("document_type").HasConversion<string>().IsRequired();
        builder.Property(t => t.Version).HasColumnName("version").IsRequired();
        builder.Property(t => t.LayoutAssetKey).HasColumnName("layout_asset_key");
        builder.Property(t => t.PublishedAt).HasColumnName("published_at").IsRequired();

        builder.HasIndex(t => new { t.DocumentType, t.Version }).IsUnique().HasDatabaseName("ux_document_templates_type_version");

        builder.HasMany(t => t.Translations)
            .WithOne()
            .HasForeignKey(t => t.TemplateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(t => t.Translations).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

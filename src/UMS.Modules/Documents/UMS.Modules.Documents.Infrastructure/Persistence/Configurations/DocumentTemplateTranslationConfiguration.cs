using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Documents.Domain.Templates;

namespace UMS.Modules.Documents.Infrastructure.Persistence.Configurations;

/// <summary>ADR-0011's <c>{table}_translations</c> pattern, keyed <c>(entity_id, language_code)</c> (ums-conventions.md, Localization Implementation).</summary>
internal sealed class DocumentTemplateTranslationConfiguration : IEntityTypeConfiguration<DocumentTemplateTranslation>
{
    public void Configure(EntityTypeBuilder<DocumentTemplateTranslation> builder)
    {
        builder.ToTable("document_template_translations");
        builder.HasKey(t => new { t.TemplateId, t.Language });

        builder.Property(t => t.TemplateId)
            .HasConversion(id => id.Value, value => new DocumentTemplateId(value))
            .HasColumnName("template_id");
        builder.Property(t => t.Language).HasColumnName("language_code").HasConversion<string>().IsRequired();
        builder.Property(t => t.Title).HasColumnName("title").IsRequired();
        builder.Property(t => t.LabelsJson).HasColumnName("labels_json").HasColumnType("jsonb").IsRequired();
    }
}

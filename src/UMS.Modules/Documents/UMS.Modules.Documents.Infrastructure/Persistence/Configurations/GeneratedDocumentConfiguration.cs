using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Documents.Domain.GeneratedDocuments;
using UMS.Modules.Documents.Domain.Templates;

namespace UMS.Modules.Documents.Infrastructure.Persistence.Configurations;

/// <summary>
/// DOC-2: the metadata-only aggregate (ADR-0010). The <c>(owner_id, document_type,
/// source_reference_id)</c> unique index is the real database enforcement of requirement-spec.md
/// documents §4's idempotent-generation invariant and edge-cases.md's claim-row race decision -
/// without it, the claim-row mechanism degrades from "the losing insert fails fast" to "a hopeful
/// application-level check with a race window."
/// </summary>
internal sealed class GeneratedDocumentConfiguration : IEntityTypeConfiguration<GeneratedDocument>
{
    public void Configure(EntityTypeBuilder<GeneratedDocument> builder)
    {
        builder.ToTable("generated_documents");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id)
            .HasConversion(id => id.Value, value => new GeneratedDocumentId(value))
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(d => d.OwnerId).HasColumnName("owner_id").IsRequired();
        builder.Property(d => d.DocumentType).HasColumnName("document_type").HasConversion<string>().IsRequired();
        builder.Property(d => d.SourceReferenceId).HasColumnName("source_reference_id").IsRequired();

        builder.Property(d => d.TemplateId)
            .HasConversion(id => id.Value, value => new DocumentTemplateId(value))
            .HasColumnName("template_id")
            .IsRequired();

        builder.Property(d => d.TemplateVersion).HasColumnName("template_version").IsRequired();
        builder.Property(d => d.Status).HasColumnName("status").HasConversion<string>().IsRequired();
        builder.Property(d => d.StorageKey).HasColumnName("storage_key");
        builder.Property(d => d.Checksum).HasColumnName("checksum");
        builder.Property(d => d.MimeType).HasColumnName("mime_type");
        builder.Property(d => d.SizeBytes).HasColumnName("size_bytes");
        builder.Property(d => d.DigitalVerificationId).HasColumnName("digital_verification_id").IsRequired();
        builder.Property(d => d.RenderDataJson).HasColumnName("render_data_json").HasColumnType("jsonb").IsRequired();
        builder.Property(d => d.Language).HasColumnName("language_code").HasConversion<string>().IsRequired();
        builder.Property(d => d.RequestedByUserId).HasColumnName("requested_by_user_id");
        builder.Property(d => d.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(d => d.ReadyAt).HasColumnName("ready_at");
        builder.Property(d => d.RevokedAt).HasColumnName("revoked_at");
        builder.Property(d => d.RevokedReason).HasColumnName("revoked_reason");

        builder.Property(d => d.SupersededByDocumentId)
            .HasConversion(id => id == null ? (Guid?)null : id.Value.Value, value => value == null ? (GeneratedDocumentId?)null : new GeneratedDocumentId(value.Value))
            .HasColumnName("superseded_by_document_id");

        builder.Property(d => d.BulkGenerationJobId).HasColumnName("bulk_generation_job_id");

        builder.HasIndex(d => new { d.OwnerId, d.DocumentType, d.SourceReferenceId }).IsUnique().HasDatabaseName("ux_generated_documents_natural_key");
        builder.HasIndex(d => d.DigitalVerificationId).IsUnique().HasDatabaseName("ux_generated_documents_verification_id");
        builder.HasIndex(d => new { d.Status, d.CreatedAt }).HasDatabaseName("ix_generated_documents_status_created_at");
        builder.HasIndex(d => d.BulkGenerationJobId).HasDatabaseName("ix_generated_documents_bulk_job_id");
    }
}

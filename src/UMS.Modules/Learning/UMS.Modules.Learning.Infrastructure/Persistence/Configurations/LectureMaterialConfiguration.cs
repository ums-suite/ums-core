using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Learning.Domain.LectureMaterials;

namespace UMS.Modules.Learning.Infrastructure.Persistence.Configurations;

internal sealed class LectureMaterialConfiguration : IEntityTypeConfiguration<LectureMaterial>
{
    public void Configure(EntityTypeBuilder<LectureMaterial> builder)
    {
        builder.ToTable("lecture_materials");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasConversion(id => id.Value, value => new LectureMaterialId(value)).ValueGeneratedNever().HasColumnName("id");

        builder.Property(m => m.CourseOfferingId).HasColumnName("course_offering_id").IsRequired();
        builder.Property(m => m.MaterialType).HasColumnName("material_type").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(m => m.ModuleGroup).HasColumnName("module_group").HasMaxLength(200).IsRequired();
        builder.Property(m => m.SortOrder).HasColumnName("sort_order").IsRequired();
        builder.Property(m => m.PublishedByUserId).HasColumnName("published_by_user_id").IsRequired();
        builder.Property(m => m.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(m => m.CurrentVersionId)
            .HasConversion(id => id!.Value.Value, value => new LectureMaterialVersionId(value))
            .HasColumnName("current_version_id");
        builder.Property(m => m.Version).HasColumnName("xmin").HasColumnType("xid").IsRowVersion().ValueGeneratedOnAddOrUpdate();
        builder.Ignore(m => m.CurrentVersion);

        builder.HasIndex(m => new { m.CourseOfferingId, m.ModuleGroup, m.SortOrder }).HasDatabaseName("ix_lecture_materials_offering_group_order");

        // requirement-spec.md §4: "LectureMaterialVersions are never deleted or mutated once
        // published." The domain type has no mutating member at all; this table has no update path
        // either - versions are only ever appended.
        builder.OwnsMany(m => m.Versions, version =>
        {
            version.ToTable("lecture_material_versions");
            version.Property(v => v.Id).HasConversion(id => id.Value, value => new LectureMaterialVersionId(value)).ValueGeneratedNever().HasColumnName("id");
            version.WithOwner().HasForeignKey(v => v.LectureMaterialId);
            version.Property(v => v.LectureMaterialId).HasConversion(id => id.Value, value => new LectureMaterialId(value)).HasColumnName("lecture_material_id");
            version.HasKey(v => v.Id);

            version.Property(v => v.VersionNumber).HasColumnName("version_number").IsRequired();
            version.Property(v => v.ArtifactId).HasColumnName("artifact_id");
            version.Property(v => v.ExternalUrl).HasColumnName("external_url").HasMaxLength(2000);
            version.Property(v => v.ChangeNote).HasColumnName("change_note").HasMaxLength(2000);
            version.Property(v => v.PublishedByUserId).HasColumnName("published_by_user_id").IsRequired();
            version.Property(v => v.PublishedAt).HasColumnName("published_at").IsRequired();

            version.HasIndex(v => new { v.LectureMaterialId, v.VersionNumber }).IsUnique().HasDatabaseName("ux_lecture_material_versions_material_number");
        });
        builder.Navigation(m => m.Versions).UsePropertyAccessMode(PropertyAccessMode.Field);

        // ums-conventions.md, Localization Implementation: the platform-wide {table}_translations
        // shape keyed (entity_id, language_code), resolved server-side with an English fallback.
        builder.OwnsMany(m => m.Translations, translation =>
        {
            translation.ToTable("lecture_material_translations");
            translation.WithOwner().HasForeignKey(t => t.LectureMaterialId);
            translation.Property(t => t.LectureMaterialId).HasConversion(id => id.Value, value => new LectureMaterialId(value)).HasColumnName("lecture_material_id");
            translation.Property(t => t.Language).HasColumnName("language_code").HasConversion<string>().HasMaxLength(2);
            translation.HasKey(t => new { t.LectureMaterialId, t.Language });

            translation.Property(t => t.Title).HasColumnName("title").HasMaxLength(500).IsRequired();
            translation.Property(t => t.Description).HasColumnName("description");
        });
        builder.Navigation(m => m.Translations).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

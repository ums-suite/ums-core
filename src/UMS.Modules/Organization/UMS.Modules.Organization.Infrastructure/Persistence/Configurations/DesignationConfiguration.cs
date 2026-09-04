using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Organization.Domain.Designations;

namespace UMS.Modules.Organization.Infrastructure.Persistence.Configurations;

/// <summary>ORG-8/ADR-0011: `designation_translations`, keyed `(entity_id, language_code)` - the translated field is a title, so the shared <see cref="Domain.Translations.NameTranslation.Name"/> property maps to a `title` column here.</summary>
internal sealed class DesignationConfiguration : IEntityTypeConfiguration<Designation>
{
    public void Configure(EntityTypeBuilder<Designation> builder)
    {
        builder.ToTable("designations");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id)
            .HasConversion(id => id.Value, value => new DesignationId(value))
            .ValueGeneratedNever()
            .HasColumnName("id");

        builder.Property(d => d.Title).HasColumnName("title").IsRequired();
        builder.Property(d => d.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.Property(d => d.Version)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsRowVersion()
            .ValueGeneratedOnAddOrUpdate();

        // No DB-level uniqueness constraint here (unlike Campus/Faculty/Department/Program's
        // `(parent_id, name)` constraints) - requirement-spec.md organization §4's uniqueness
        // invariant is scoped explicitly to the parent-chain levels; Designation is a flat,
        // parent-less list, and no edge case or design decision names a duplicate-title race for
        // it. DesignationService's own pre-check (`IDesignationRepository.ExistsByTitleAsync`) is
        // a best-effort, not a DB-enforced guarantee - an accepted, narrower scope than the
        // hierarchy's own uniqueness mechanism.
        builder.OwnsMany(d => d.Translations, translation =>
        {
            translation.ToTable("designation_translations");
            translation.WithOwner().HasForeignKey("DesignationId");
            translation.HasKey("DesignationId", "LanguageCode");
            translation.Property<DesignationId>("DesignationId")
                .HasConversion(id => id.Value, value => new DesignationId(value))
                .HasColumnName("designation_id");
            translation.Property(t => t.LanguageCode).HasColumnName("language_code").HasMaxLength(10).IsRequired();
            translation.Property(t => t.Name).HasColumnName("title").IsRequired();
        });
        builder.Navigation(d => d.Translations).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

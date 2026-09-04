using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Organization.Domain.Campuses;
using UMS.Modules.Organization.Domain.Faculties;

namespace UMS.Modules.Organization.Infrastructure.Persistence.Configurations;

/// <summary>ORG-8/ADR-0011: `faculty_translations`, keyed `(entity_id, language_code)` - the companion translation table pattern.</summary>
internal sealed class FacultyConfiguration : IEntityTypeConfiguration<Faculty>
{
    public void Configure(EntityTypeBuilder<Faculty> builder)
    {
        builder.ToTable("faculties");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id)
            .HasConversion(id => id.Value, value => new FacultyId(value))
            .ValueGeneratedNever()
            .HasColumnName("id");

        builder.Property(f => f.CampusId)
            .HasConversion(id => id.Value, value => new CampusId(value))
            .HasColumnName("campus_id")
            .IsRequired();

        builder.Property(f => f.Name).HasColumnName("name").IsRequired();
        builder.Property(f => f.Status).HasColumnName("status").HasConversion<string>().IsRequired();
        builder.Property(f => f.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.Property(f => f.Version)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsRowVersion()
            .ValueGeneratedOnAddOrUpdate();

        builder.HasIndex(f => new { f.CampusId, f.Name }).IsUnique().HasDatabaseName("ux_faculties_campus_id_name");
        builder.HasIndex(f => f.CampusId).HasDatabaseName("ix_faculties_campus_id");

        builder.OwnsMany(f => f.Translations, translation =>
        {
            translation.ToTable("faculty_translations");
            translation.WithOwner().HasForeignKey("FacultyId");
            translation.HasKey("FacultyId", "LanguageCode");
            translation.Property<FacultyId>("FacultyId")
                .HasConversion(id => id.Value, value => new FacultyId(value))
                .HasColumnName("faculty_id");
            translation.Property(t => t.LanguageCode).HasColumnName("language_code").HasMaxLength(10).IsRequired();
            translation.Property(t => t.Name).HasColumnName("name").IsRequired();
        });
        builder.Navigation(f => f.Translations).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

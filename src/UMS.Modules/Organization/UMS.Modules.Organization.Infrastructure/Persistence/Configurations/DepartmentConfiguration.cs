using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Organization.Domain.Departments;
using UMS.Modules.Organization.Domain.Faculties;

namespace UMS.Modules.Organization.Infrastructure.Persistence.Configurations;

/// <summary>ORG-8/ADR-0011: `department_translations`, keyed `(entity_id, language_code)`.</summary>
internal sealed class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.ToTable("departments");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id)
            .HasConversion(id => id.Value, value => new DepartmentId(value))
            .ValueGeneratedNever()
            .HasColumnName("id");

        builder.Property(d => d.FacultyId)
            .HasConversion(id => id.Value, value => new FacultyId(value))
            .HasColumnName("faculty_id")
            .IsRequired();

        builder.Property(d => d.Name).HasColumnName("name").IsRequired();
        builder.Property(d => d.Status).HasColumnName("status").HasConversion<string>().IsRequired();
        builder.Property(d => d.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.Property(d => d.Version)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsRowVersion()
            .ValueGeneratedOnAddOrUpdate();

        builder.HasIndex(d => new { d.FacultyId, d.Name }).IsUnique().HasDatabaseName("ux_departments_faculty_id_name");
        builder.HasIndex(d => d.FacultyId).HasDatabaseName("ix_departments_faculty_id");

        builder.OwnsMany(d => d.Translations, translation =>
        {
            translation.ToTable("department_translations");
            translation.WithOwner().HasForeignKey("DepartmentId");
            translation.HasKey("DepartmentId", "LanguageCode");
            translation.Property<DepartmentId>("DepartmentId")
                .HasConversion(id => id.Value, value => new DepartmentId(value))
                .HasColumnName("department_id");
            translation.Property(t => t.LanguageCode).HasColumnName("language_code").HasMaxLength(10).IsRequired();
            translation.Property(t => t.Name).HasColumnName("name").IsRequired();
        });
        builder.Navigation(d => d.Translations).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

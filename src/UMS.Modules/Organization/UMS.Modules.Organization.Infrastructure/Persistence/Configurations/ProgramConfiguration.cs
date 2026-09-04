using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Organization.Domain.Departments;
using OrgProgram = UMS.Modules.Organization.Domain.Programs.Program;
using OrgProgramId = UMS.Modules.Organization.Domain.Programs.ProgramId;

namespace UMS.Modules.Organization.Infrastructure.Persistence.Configurations;

/// <summary>ORG-8/ADR-0011: `program_translations`, keyed `(entity_id, language_code)`.</summary>
internal sealed class ProgramConfiguration : IEntityTypeConfiguration<OrgProgram>
{
    public void Configure(EntityTypeBuilder<OrgProgram> builder)
    {
        builder.ToTable("programs");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id)
            .HasConversion(id => id.Value, value => new OrgProgramId(value))
            .ValueGeneratedNever()
            .HasColumnName("id");

        builder.Property(p => p.DepartmentId)
            .HasConversion(id => id.Value, value => new DepartmentId(value))
            .HasColumnName("department_id")
            .IsRequired();

        builder.Property(p => p.Name).HasColumnName("name").IsRequired();
        builder.Property(p => p.Status).HasColumnName("status").HasConversion<string>().IsRequired();
        builder.Property(p => p.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.Property(p => p.Version)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsRowVersion()
            .ValueGeneratedOnAddOrUpdate();

        builder.HasIndex(p => new { p.DepartmentId, p.Name }).IsUnique().HasDatabaseName("ux_programs_department_id_name");
        builder.HasIndex(p => p.DepartmentId).HasDatabaseName("ix_programs_department_id");

        builder.OwnsMany(p => p.Translations, translation =>
        {
            translation.ToTable("program_translations");
            translation.WithOwner().HasForeignKey("ProgramId");
            translation.HasKey("ProgramId", "LanguageCode");
            translation.Property<OrgProgramId>("ProgramId")
                .HasConversion(id => id.Value, value => new OrgProgramId(value))
                .HasColumnName("program_id");
            translation.Property(t => t.LanguageCode).HasColumnName("language_code").HasMaxLength(10).IsRequired();
            translation.Property(t => t.Name).HasColumnName("name").IsRequired();
        });
        builder.Navigation(p => p.Translations).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

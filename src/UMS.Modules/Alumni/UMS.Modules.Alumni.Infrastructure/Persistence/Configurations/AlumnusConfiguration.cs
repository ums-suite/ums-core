using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Alumni.Domain.Alumni;

namespace UMS.Modules.Alumni.Infrastructure.Persistence.Configurations;

internal sealed class AlumnusConfiguration : IEntityTypeConfiguration<Alumnus>
{
    public void Configure(EntityTypeBuilder<Alumnus> builder)
    {
        builder.ToTable("alumni");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasConversion(id => id.Value, value => new AlumnusId(value)).ValueGeneratedNever().HasColumnName("id");

        builder.Property(a => a.StudentIdRef).HasColumnName("student_id_ref").IsRequired();
        builder.Property(a => a.GraduationYear).HasColumnName("graduation_year").IsRequired();
        builder.Property(a => a.ProgramId).HasColumnName("program_id").IsRequired();
        builder.Property(a => a.DepartmentId).HasColumnName("department_id").IsRequired();
        builder.Property(a => a.ProfileVisibility).HasColumnName("profile_visibility").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(a => a.CurrentEmployer).HasColumnName("current_employer").HasMaxLength(300);
        builder.Property(a => a.Bio).HasColumnName("bio").HasMaxLength(4000);
        builder.Property(a => a.Location).HasColumnName("location").HasMaxLength(300);
        builder.Property(a => a.ContactEmail).HasColumnName("contact_email").HasMaxLength(320);
        builder.Property(a => a.ContactPhone).HasColumnName("contact_phone").HasMaxLength(30);
        builder.Property(a => a.HideCurrentEmployer).HasColumnName("hide_current_employer").IsRequired();
        builder.Property(a => a.HideContactDetails).HasColumnName("hide_contact_details").IsRequired();
        builder.Property(a => a.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(a => a.Version).HasColumnName("xmin").HasColumnType("xid").IsRowVersion().ValueGeneratedOnAddOrUpdate();

        // design-decisions.md "Idempotent StudentGraduated Consumption" / requirement-spec.md §4: the
        // ONLY correctness mechanism for "one Alumnus per Student" - a DB-level UNIQUE constraint, not
        // application logic. AlumnusService explicitly catches the resulting unique-violation
        // (translated to DuplicateValueException by AlumniDbContext) and returns the existing row.
        builder.HasIndex(a => a.StudentIdRef).IsUnique().HasDatabaseName("ux_alumni_student_id_ref");

        builder.HasIndex(a => a.GraduationYear).HasDatabaseName("ix_alumni_graduation_year");
        builder.HasIndex(a => a.ProgramId).HasDatabaseName("ix_alumni_program_id");
        builder.HasIndex(a => a.DepartmentId).HasDatabaseName("ix_alumni_department_id");
        builder.HasIndex(a => a.ProfileVisibility).HasDatabaseName("ix_alumni_profile_visibility");
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Student.Domain.Guardians;
using UMS.Modules.Student.Domain.Students;
using UMS.Shared.Domain;

namespace UMS.Modules.Student.Infrastructure.Persistence.Configurations;

internal sealed class StudentConfiguration : IEntityTypeConfiguration<Domain.Students.Student>
{
    public void Configure(EntityTypeBuilder<Domain.Students.Student> builder)
    {
        builder.ToTable("students");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id)
            .HasConversion(id => id.Value, value => new StudentId(value))
            .ValueGeneratedNever()
            .HasColumnName("id");

        builder.Property(s => s.OriginatingApplicationId).HasColumnName("originating_application_id").IsRequired();
        builder.HasIndex(s => s.OriginatingApplicationId).IsUnique().HasDatabaseName("ux_students_originating_application_id");

        builder.Property(s => s.DepartmentId).HasColumnName("department_id").IsRequired();
        builder.HasIndex(s => s.DepartmentId).HasDatabaseName("ix_students_department_id");

        builder.Property(s => s.ProgramId).HasColumnName("program_id").IsRequired();

        builder.Property(s => s.Status).HasColumnName("status").HasConversion<string>().IsRequired();

        builder.Property(s => s.IdentityUserId).HasColumnName("identity_user_id");
        builder.HasIndex(s => s.IdentityUserId).IsUnique().HasDatabaseName("ux_students_identity_user_id").HasFilter("\"identity_user_id\" IS NOT NULL");

        builder.Property(s => s.IdCardDocumentId).HasColumnName("id_card_document_id");

        builder.Property(s => s.DateOfBirth).HasColumnName("date_of_birth").IsRequired();
        builder.Property(s => s.NationalId).HasColumnName("national_id");

        builder.Property(s => s.ContactEmail).HasColumnName("contact_email");
        builder.Property(s => s.ContactPhone).HasColumnName("contact_phone");
        builder.Property(s => s.PhotoUrl).HasColumnName("photo_url");

        builder.Property(s => s.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.Property(s => s.Version)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsRowVersion()
            .ValueGeneratedOnAddOrUpdate();

        builder.Property(s => s.Email)
            .HasConversion(email => email.Value, value => Email.Create(value).Value)
            .HasColumnName("email")
            .HasColumnType("citext")
            .IsRequired();

        builder.Property(s => s.Mobile)
            .HasConversion(
                mobile => mobile == null ? null : mobile.Value,
                value => value == null ? null : PhoneNumber.Create(value).Value)
            .HasColumnName("mobile")
            .HasColumnType("citext");

        builder.OwnsOne(s => s.Name, name =>
        {
            name.Property(n => n.GivenName).HasColumnName("given_name").IsRequired();
            name.Property(n => n.FamilyName).HasColumnName("family_name").IsRequired();
            name.Property(n => n.GivenNameBn).HasColumnName("given_name_bn");
            name.Property(n => n.FamilyNameBn).HasColumnName("family_name_bn");
        });
        builder.Navigation(s => s.Name).IsRequired();

        builder.OwnsOne(s => s.StudentNumber, studentNumber =>
        {
            studentNumber.Property(n => n.Value).HasColumnName("student_number").IsRequired();
            studentNumber.Property(n => n.AdmissionYear).HasColumnName("admission_year").IsRequired();
            studentNumber.Property(n => n.FacultyCode).HasColumnName("faculty_code").IsRequired();
            studentNumber.Property(n => n.Sequence).HasColumnName("student_number_sequence").IsRequired();
            studentNumber.HasIndex(n => n.Value).IsUnique().HasDatabaseName("ux_students_student_number");
        });
        builder.Navigation(s => s.StudentNumber).IsRequired();

        ConfigureStatusHistory(builder);
        ConfigureGuardians(builder);
        ConfigureGuardianAccessGrants(builder);
    }

    private static void ConfigureStatusHistory(EntityTypeBuilder<Domain.Students.Student> builder)
    {
        builder.OwnsMany(s => s.StatusHistory, history =>
        {
            history.ToTable("student_status_history");
            history.Property<StudentId>("StudentId")
                .HasConversion(id => id.Value, value => new StudentId(value))
                .HasColumnName("student_id");
            history.WithOwner().HasForeignKey("StudentId");
            history.HasKey(h => h.Id);
            history.Property(h => h.Id)
                .HasConversion(id => id.Value, value => new StudentStatusHistoryEntryId(value))
                .ValueGeneratedNever()
                .HasColumnName("id");

            history.Property(h => h.FromStatus).HasColumnName("from_status").HasConversion<string>();
            history.Property(h => h.ToStatus).HasColumnName("to_status").HasConversion<string>().IsRequired();
            history.Property(h => h.Reason).HasColumnName("reason");
            history.Property(h => h.ChangedByUserId).HasColumnName("changed_by_user_id");
            history.Property(h => h.ChangedAt).HasColumnName("changed_at").IsRequired();

            history.HasIndex("StudentId").HasDatabaseName("ix_student_status_history_student_id");
        });
        builder.Navigation(s => s.StatusHistory).UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    private static void ConfigureGuardians(EntityTypeBuilder<Domain.Students.Student> builder)
    {
        builder.OwnsMany(s => s.Guardians, guardian =>
        {
            guardian.ToTable("guardians");
            guardian.WithOwner().HasForeignKey("StudentId");
            guardian.HasKey(g => g.Id);
            guardian.Property(g => g.Id)
                .HasConversion(id => id.Value, value => new GuardianId(value))
                .ValueGeneratedNever()
                .HasColumnName("id");

            guardian.Property(g => g.StudentId)
                .HasConversion(id => id.Value, value => new StudentId(value))
                .HasColumnName("student_id")
                .IsRequired();
            guardian.Property(g => g.Name).HasColumnName("name").IsRequired();
            guardian.Property(g => g.Relationship).HasColumnName("relationship").IsRequired();
            guardian.Property(g => g.ContactEmail).HasColumnName("contact_email");
            guardian.Property(g => g.ContactPhone).HasColumnName("contact_phone");
            guardian.Property(g => g.LinkedAt).HasColumnName("linked_at").IsRequired();

            guardian.HasIndex("StudentId").HasDatabaseName("ix_guardians_student_id");
        });
        builder.Navigation(s => s.Guardians).UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    private static void ConfigureGuardianAccessGrants(EntityTypeBuilder<Domain.Students.Student> builder)
    {
        builder.OwnsMany(s => s.GuardianAccessGrants, grant =>
        {
            grant.ToTable("guardian_access_grants");
            grant.WithOwner().HasForeignKey("StudentId");
            grant.HasKey(g => g.Id);
            grant.Property(g => g.Id)
                .HasConversion(id => id.Value, value => new GuardianAccessGrantId(value))
                .ValueGeneratedNever()
                .HasColumnName("id");

            grant.Property(g => g.GuardianId).HasColumnName("guardian_id").IsRequired();
            grant.Property(g => g.StudentId)
                .HasConversion(id => id.Value, value => new StudentId(value))
                .HasColumnName("student_id")
                .IsRequired();
            grant.Property(g => g.Category).HasColumnName("category").HasConversion<string>().IsRequired();
            grant.Property(g => g.GrantedAt).HasColumnName("granted_at").IsRequired();
            grant.Property(g => g.RevokedAt).HasColumnName("revoked_at");

            grant.HasIndex("StudentId", "GuardianId").HasDatabaseName("ix_guardian_access_grants_student_guardian");
        });
        builder.Navigation(s => s.GuardianAccessGrants).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

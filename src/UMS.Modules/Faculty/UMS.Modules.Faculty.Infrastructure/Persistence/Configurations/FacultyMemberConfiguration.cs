using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Faculty.Domain.FacultyMembers;

namespace UMS.Modules.Faculty.Infrastructure.Persistence.Configurations;

internal sealed class FacultyMemberConfiguration : IEntityTypeConfiguration<FacultyMember>
{
    public void Configure(EntityTypeBuilder<FacultyMember> builder)
    {
        builder.ToTable("faculty_members");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id)
            .HasConversion(id => id.Value, value => new FacultyMemberId(value))
            .ValueGeneratedNever()
            .HasColumnName("id");

        builder.Property(f => f.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(f => f.EmployeeId).HasColumnName("employee_id").IsRequired();
        builder.Property(f => f.DepartmentId).HasColumnName("department_id").IsRequired();
        builder.Property(f => f.DesignationId).HasColumnName("designation_id").IsRequired();
        builder.Property(f => f.EmploymentType).HasColumnName("employment_type").HasConversion<string>().IsRequired();
        builder.Property(f => f.Status).HasColumnName("status").HasConversion<string>().IsRequired();
        builder.Property(f => f.IsDepartmentHead).HasColumnName("is_department_head").IsRequired();
        builder.Property(f => f.ContactEmail).HasColumnName("contact_email");
        builder.Property(f => f.ContactPhone).HasColumnName("contact_phone");
        builder.Property(f => f.JoiningDate).HasColumnName("joining_date").IsRequired();
        builder.Property(f => f.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.Property(f => f.Version)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsRowVersion()
            .ValueGeneratedOnAddOrUpdate();

        builder.HasIndex(f => f.EmployeeId).IsUnique().HasDatabaseName("ux_faculty_members_employee_id");
        builder.HasIndex(f => f.UserId).IsUnique().HasDatabaseName("ux_faculty_members_user_id");
        builder.HasIndex(f => f.DepartmentId).HasDatabaseName("ix_faculty_members_department_id");
    }
}

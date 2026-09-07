using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Academic.Domain.Courses;

namespace UMS.Modules.Academic.Infrastructure.Persistence.Configurations;

internal sealed class CourseConfiguration : IEntityTypeConfiguration<Course>
{
    public void Configure(EntityTypeBuilder<Course> builder)
    {
        builder.ToTable("courses");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasConversion(id => id.Value, value => new CourseId(value)).ValueGeneratedNever().HasColumnName("id");

        builder.Property(c => c.Code).HasColumnName("code").IsRequired();
        builder.HasIndex(c => c.Code).IsUnique().HasDatabaseName("ux_courses_code");

        builder.Property(c => c.Title).HasColumnName("title").IsRequired();
        builder.Property(c => c.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.Property(c => c.Version).HasColumnName("xmin").HasColumnType("xid").IsRowVersion().ValueGeneratedOnAddOrUpdate();

        builder.OwnsOne(c => c.CreditHours, credit =>
        {
            credit.Property(v => v.Value).HasColumnName("credit_hours").IsRequired();
        });
        builder.Navigation(c => c.CreditHours).IsRequired();

        builder.OwnsMany(c => c.Prerequisites, prerequisite =>
        {
            prerequisite.ToTable("course_prerequisites");
            prerequisite.Property(p => p.CourseId).HasConversion(id => id.Value, value => new CourseId(value)).HasColumnName("course_id");
            prerequisite.Property(p => p.PrerequisiteCourseId).HasConversion(id => id.Value, value => new CourseId(value)).HasColumnName("prerequisite_course_id");
            prerequisite.WithOwner().HasForeignKey("CourseId");
            prerequisite.HasKey("CourseId", "PrerequisiteCourseId");
        });
        builder.Navigation(c => c.Prerequisites).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

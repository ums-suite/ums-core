using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Academic.Domain.Courses;
using UMS.Modules.Academic.Domain.Curricula;

namespace UMS.Modules.Academic.Infrastructure.Persistence.Configurations;

internal sealed class CurriculumConfiguration : IEntityTypeConfiguration<Curriculum>
{
    public void Configure(EntityTypeBuilder<Curriculum> builder)
    {
        builder.ToTable("curricula");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasConversion(id => id.Value, value => new CurriculumId(value)).ValueGeneratedNever().HasColumnName("id");

        builder.Property(c => c.ProgramId).HasColumnName("program_id").IsRequired();
        builder.Property(c => c.CurriculumVersion).HasColumnName("curriculum_version").IsRequired();
        builder.HasIndex(c => new { c.ProgramId, c.CurriculumVersion }).IsUnique().HasDatabaseName("ux_curricula_program_version");

        builder.Property(c => c.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(c => c.Version).HasColumnName("xmin").HasColumnType("xid").IsRowVersion().ValueGeneratedOnAddOrUpdate();

        builder.OwnsMany(c => c.Courses, course =>
        {
            course.ToTable("curriculum_courses");
            course.Property<CurriculumId>("CurriculumId").HasConversion(id => id.Value, value => new CurriculumId(value)).HasColumnName("curriculum_id");
            course.Property(cc => cc.CourseId).HasConversion(id => id.Value, value => new CourseId(value)).HasColumnName("course_id");
            course.Property(cc => cc.IsRequired).HasColumnName("is_required").IsRequired();
            course.WithOwner().HasForeignKey("CurriculumId");
            course.HasKey("CurriculumId", "CourseId");
        });
        builder.Navigation(c => c.Courses).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

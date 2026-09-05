using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Academic.Domain.CourseOfferings;

namespace UMS.Modules.Academic.Infrastructure.Persistence.Configurations;

internal sealed class CourseOfferingConfiguration : IEntityTypeConfiguration<CourseOffering>
{
    public void Configure(EntityTypeBuilder<CourseOffering> builder)
    {
        builder.ToTable("course_offerings");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).HasConversion(id => id.Value, value => new CourseOfferingId(value)).ValueGeneratedNever().HasColumnName("id");

        builder.Property(o => o.CourseId).HasColumnName("course_id").IsRequired();
        builder.Property(o => o.SemesterId).HasColumnName("semester_id").IsRequired();
        builder.HasIndex(o => o.SemesterId).HasDatabaseName("ix_course_offerings_semester_id");

        builder.Property(o => o.DepartmentId).HasColumnName("department_id").IsRequired();
        builder.Property(o => o.Capacity).HasColumnName("capacity").IsRequired();

        // ACD-6's seat-limit counter - see CourseOffering's own class remarks: mutated ONLY via
        // ICourseOfferingRepository's atomic conditional UPDATE (ExecuteUpdateAsync), never through
        // this entity's own tracked SaveChanges path in the enrollment/drop flows.
        builder.Property(o => o.EnrolledCount).HasColumnName("enrolled_count").IsRequired();

        builder.Property(o => o.InstructorFacultyMemberId).HasColumnName("instructor_faculty_member_id");
        builder.Property(o => o.InstructorAssignedAt).HasColumnName("instructor_assigned_at");

        builder.Property(o => o.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(o => o.Version).HasColumnName("xmin").HasColumnType("xid").IsRowVersion().ValueGeneratedOnAddOrUpdate();

        builder.OwnsMany(o => o.Sections, section =>
        {
            section.ToTable("sections");
            section.Property(s => s.Id).HasConversion(id => id.Value, value => new SectionId(value)).ValueGeneratedNever().HasColumnName("id");
            section.Property<CourseOfferingId>("CourseOfferingId").HasConversion(id => id.Value, value => new CourseOfferingId(value)).HasColumnName("course_offering_id");
            section.WithOwner().HasForeignKey("CourseOfferingId");
            section.HasKey(s => s.Id);

            section.Property(s => s.Code).HasColumnName("code").IsRequired();

            section.OwnsOne(s => s.Schedule, schedule =>
            {
                schedule.Property(w => w.DayOfWeek).HasColumnName("day_of_week").IsRequired();
                schedule.Property(w => w.Start).HasColumnName("start_time").IsRequired();
                schedule.Property(w => w.End).HasColumnName("end_time").IsRequired();
            });
            section.Navigation(s => s.Schedule).IsRequired();

            section.HasIndex("CourseOfferingId").HasDatabaseName("ix_sections_course_offering_id");
        });
        builder.Navigation(o => o.Sections).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(o => o.Exams, exam =>
        {
            exam.ToTable("exams");
            exam.Property(e => e.Id).HasConversion(id => id.Value, value => new ExamId(value)).ValueGeneratedNever().HasColumnName("id");
            exam.Property<CourseOfferingId>("CourseOfferingId").HasConversion(id => id.Value, value => new CourseOfferingId(value)).HasColumnName("course_offering_id");
            exam.WithOwner().HasForeignKey("CourseOfferingId");
            exam.HasKey(e => e.Id);

            exam.Property(e => e.Name).HasColumnName("name").IsRequired();
            exam.HasIndex("CourseOfferingId").HasDatabaseName("ix_exams_course_offering_id");

            exam.OwnsMany(e => e.Assessments, assessment =>
            {
                assessment.ToTable("assessments");
                assessment.Property(a => a.Id).HasConversion(id => id.Value, value => new AssessmentId(value)).ValueGeneratedNever().HasColumnName("id");
                assessment.Property<ExamId>("ExamId").HasConversion(id => id.Value, value => new ExamId(value)).HasColumnName("exam_id");
                assessment.WithOwner().HasForeignKey("ExamId");
                assessment.HasKey(a => a.Id);

                assessment.Property(a => a.Name).HasColumnName("name").IsRequired();
                assessment.Property(a => a.Weight).HasColumnName("weight").HasColumnType("numeric(5,4)").IsRequired();
                assessment.HasIndex("ExamId").HasDatabaseName("ix_assessments_exam_id");
            });
            exam.Navigation(e => e.Assessments).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
        builder.Navigation(o => o.Exams).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

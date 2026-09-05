using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Academic.Domain.Attendance;

namespace UMS.Modules.Academic.Infrastructure.Persistence.Configurations;

internal sealed class AttendanceSessionConfiguration : IEntityTypeConfiguration<AttendanceSession>
{
    public void Configure(EntityTypeBuilder<AttendanceSession> builder)
    {
        builder.ToTable("attendance_sessions");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasConversion(id => id.Value, value => new AttendanceSessionId(value)).ValueGeneratedNever().HasColumnName("id");

        builder.Property(s => s.CourseOfferingId).HasColumnName("course_offering_id").IsRequired();
        builder.Property(s => s.SessionDate).HasColumnName("session_date").IsRequired();
        builder.HasIndex(s => new { s.CourseOfferingId, s.SessionDate }).IsUnique().HasDatabaseName("ux_attendance_sessions_courseoffering_date");

        builder.Property(s => s.CorrectionWindowClose).HasColumnName("correction_window_close").IsRequired();
        builder.Property(s => s.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(s => s.Version).HasColumnName("xmin").HasColumnType("xid").IsRowVersion().ValueGeneratedOnAddOrUpdate();

        builder.OwnsMany(s => s.Records, record =>
        {
            record.ToTable("attendance_records");
            record.Property(r => r.Id).ValueGeneratedNever().HasColumnName("id");
            record.Property<AttendanceSessionId>("AttendanceSessionId").HasConversion(id => id.Value, value => new AttendanceSessionId(value)).HasColumnName("attendance_session_id");
            record.WithOwner().HasForeignKey("AttendanceSessionId");
            record.HasKey(r => r.Id);

            record.Property(r => r.EnrollmentId).HasColumnName("enrollment_id").IsRequired();
            record.Property(r => r.Status).HasColumnName("status").HasConversion<string>().IsRequired();
            record.Property(r => r.MarkedByFacultyMemberId).HasColumnName("marked_by_faculty_member_id").IsRequired();
            record.Property(r => r.MarkedAt).HasColumnName("marked_at").IsRequired();

            record.HasIndex("AttendanceSessionId", "EnrollmentId").IsUnique().HasDatabaseName("ux_attendance_records_session_enrollment");
        });
        builder.Navigation(s => s.Records).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Faculty.Domain.CourseAssignments;

namespace UMS.Modules.Faculty.Infrastructure.Persistence.Configurations;

internal sealed class CourseAssignmentConfiguration : IEntityTypeConfiguration<CourseAssignment>
{
    public void Configure(EntityTypeBuilder<CourseAssignment> builder)
    {
        builder.ToTable("course_assignments");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .HasConversion(id => id.Value, value => new CourseAssignmentId(value))
            .ValueGeneratedNever()
            .HasColumnName("id");

        builder.Property(c => c.FacultyMemberId).HasColumnName("faculty_member_id").IsRequired();
        builder.Property(c => c.CourseOfferingId).HasColumnName("course_offering_id").IsRequired();
        builder.Property(c => c.Status).HasColumnName("status").HasConversion<string>().IsRequired();
        builder.Property(c => c.AssignedAt).HasColumnName("assigned_at").IsRequired();
        builder.Property(c => c.EndedAt).HasColumnName("ended_at");
        builder.Property(c => c.LastAppliedEventOccurredAt).HasColumnName("last_applied_event_occurred_at").IsRequired();

        builder.Property(c => c.Version)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsRowVersion()
            .ValueGeneratedOnAddOrUpdate();

        // The idempotent-upsert key (design-decisions.md, "Event Consumer Idempotency and
        // Ordering") - at most one projection row per pair, matching requirement-spec.md §4's
        // "exactly one active projection per pair" invariant (Active-vs-Ended is a status on this
        // one row, not two rows).
        builder.HasIndex(c => new { c.FacultyMemberId, c.CourseOfferingId }).IsUnique().HasDatabaseName("ux_course_assignments_faculty_member_id_course_offering_id");
        builder.HasIndex(c => c.FacultyMemberId).HasDatabaseName("ix_course_assignments_faculty_member_id");
    }
}

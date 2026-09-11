using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Career.Domain.Drives;

namespace UMS.Modules.Career.Infrastructure.Persistence.Configurations;

/// <summary>
/// ums-core-gotchas: mapped as a plain, normal one-to-many-by-FK entity - deliberately NOT an EF
/// `OwnsMany` owned collection of `CampusRecruitmentDrive` (see `InterviewSlot`'s own remarks for why:
/// EF Core 10.0.11 misfires an UPDATE instead of an INSERT for a brand-new owned-collection child row
/// keyed on a caller-supplied business value, on an already-persisted parent).
///
/// <para>
/// design-decisions.md "Interview-Slot Booking Concurrency Control": the `ck_interview_slots_capacity`
/// CHECK constraint is the DB-level defense-in-depth backing
/// <see cref="Repositories.InterviewSlotRepository.TryClaimSlotAsync"/>'s own atomic conditional UPDATE.
/// </para>
/// </summary>
internal sealed class InterviewSlotConfiguration : IEntityTypeConfiguration<InterviewSlot>
{
    public void Configure(EntityTypeBuilder<InterviewSlot> builder)
    {
        builder.ToTable("interview_slots", t => t.HasCheckConstraint("ck_interview_slots_capacity", "booked_count <= capacity AND booked_count >= 0"));
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasConversion(id => id.Value, value => new InterviewSlotId(value)).ValueGeneratedNever().HasColumnName("id");

        builder.Property(s => s.DriveId).HasConversion(id => id.Value, value => new CampusRecruitmentDriveId(value)).HasColumnName("drive_id").IsRequired();
        builder.Property(s => s.StartTime).HasColumnName("start_time").IsRequired();
        builder.Property(s => s.EndTime).HasColumnName("end_time").IsRequired();
        builder.Property(s => s.Capacity).HasColumnName("capacity").IsRequired();
        builder.Property(s => s.BookedCount).HasColumnName("booked_count").IsRequired();
        builder.Property(s => s.IsCancelled).HasColumnName("is_cancelled").IsRequired();

        builder.HasIndex(s => s.DriveId).HasDatabaseName("ix_interview_slots_drive_id");
    }
}

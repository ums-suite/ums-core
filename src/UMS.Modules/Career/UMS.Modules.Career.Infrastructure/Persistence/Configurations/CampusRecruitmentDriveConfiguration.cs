using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Career.Domain.Drives;

namespace UMS.Modules.Career.Infrastructure.Persistence.Configurations;

internal sealed class CampusRecruitmentDriveConfiguration : IEntityTypeConfiguration<CampusRecruitmentDrive>
{
    public void Configure(EntityTypeBuilder<CampusRecruitmentDrive> builder)
    {
        builder.ToTable("campus_recruitment_drives");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasConversion(id => id.Value, value => new CampusRecruitmentDriveId(value)).ValueGeneratedNever().HasColumnName("id");

        builder.Property(d => d.EmployerProfileId).HasColumnName("employer_profile_id").IsRequired();
        builder.Property(d => d.Title).HasColumnName("title").HasMaxLength(300).IsRequired();
        builder.Property(d => d.VenueRoomId).HasColumnName("venue_room_id").IsRequired();
        builder.Property(d => d.ScheduledDate).HasColumnName("scheduled_date").IsRequired();
        builder.Property(d => d.RegistrationOpensAt).HasColumnName("registration_opens_at").IsRequired();
        builder.Property(d => d.RegistrationClosesAt).HasColumnName("registration_closes_at").IsRequired();
        builder.Property(d => d.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(d => d.CancellationReason).HasColumnName("cancellation_reason").HasMaxLength(1000);
        builder.Property(d => d.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(d => d.Version).HasColumnName("xmin").HasColumnType("xid").IsRowVersion().ValueGeneratedOnAddOrUpdate();

        builder.HasIndex(d => d.Status).HasDatabaseName("ix_campus_recruitment_drives_status");
        builder.HasIndex(d => d.EmployerProfileId).HasDatabaseName("ix_campus_recruitment_drives_employer_profile_id");
    }
}

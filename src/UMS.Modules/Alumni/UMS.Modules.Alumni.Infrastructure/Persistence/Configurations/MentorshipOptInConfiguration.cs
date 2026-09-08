using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Alumni.Domain.Mentorship;

namespace UMS.Modules.Alumni.Infrastructure.Persistence.Configurations;

internal sealed class MentorshipOptInConfiguration : IEntityTypeConfiguration<MentorshipOptIn>
{
    public void Configure(EntityTypeBuilder<MentorshipOptIn> builder)
    {
        builder.ToTable("mentorship_opt_ins");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).HasConversion(id => id.Value, value => new MentorshipOptInId(value)).ValueGeneratedNever().HasColumnName("id");

        builder.Property(o => o.PersonId).HasColumnName("person_id").IsRequired();
        builder.Property(o => o.Role).HasColumnName("role").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(o => o.ExpertiseAreas).HasColumnName("expertise_areas").HasMaxLength(2000);
        builder.Property(o => o.CapacityLimit).HasColumnName("capacity_limit").IsRequired();
        builder.Property(o => o.ActiveCount).HasColumnName("active_count").IsRequired();
        builder.Property(o => o.Availability).HasColumnName("availability").HasMaxLength(500);
        builder.Property(o => o.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(o => o.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(o => o.Version).HasColumnName("xmin").HasColumnType("xid").IsRowVersion().ValueGeneratedOnAddOrUpdate();

        // design-decisions.md "Mentor-Capacity Enforcement Mechanism": one opt-in row per
        // (PersonId, Role) - the atomic conditional write's own WHERE target.
        builder.HasIndex(o => new { o.PersonId, o.Role }).IsUnique().HasDatabaseName("ux_mentorship_opt_ins_person_role");
    }
}

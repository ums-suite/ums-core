using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Organization.Domain.Campuses;
using UMS.Modules.Organization.Domain.Universities;

namespace UMS.Modules.Organization.Infrastructure.Persistence.Configurations;

/// <summary>edge-cases.md "Uniqueness Enforcement Mechanism": `(university_id, name)` is a DB-level composite unique constraint, not an application-level check.</summary>
internal sealed class CampusConfiguration : IEntityTypeConfiguration<Campus>
{
    public void Configure(EntityTypeBuilder<Campus> builder)
    {
        builder.ToTable("campuses");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .HasConversion(id => id.Value, value => new CampusId(value))
            .ValueGeneratedNever()
            .HasColumnName("id");

        builder.Property(c => c.UniversityId)
            .HasConversion(id => id.Value, value => new UniversityId(value))
            .HasColumnName("university_id")
            .IsRequired();

        builder.Property(c => c.Name).HasColumnName("name").IsRequired();
        builder.Property(c => c.Status).HasColumnName("status").HasConversion<string>().IsRequired();
        builder.Property(c => c.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.Property(c => c.Version)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsRowVersion()
            .ValueGeneratedOnAddOrUpdate();

        builder.HasIndex(c => new { c.UniversityId, c.Name }).IsUnique().HasDatabaseName("ux_campuses_university_id_name");
        builder.HasIndex(c => c.UniversityId).HasDatabaseName("ix_campuses_university_id");
    }
}

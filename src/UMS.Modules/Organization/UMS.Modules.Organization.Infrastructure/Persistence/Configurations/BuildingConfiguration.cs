using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Organization.Domain.Campuses;
using UMS.Modules.Organization.Domain.Facilities;

namespace UMS.Modules.Organization.Infrastructure.Persistence.Configurations;

internal sealed class BuildingConfiguration : IEntityTypeConfiguration<Building>
{
    public void Configure(EntityTypeBuilder<Building> builder)
    {
        builder.ToTable("buildings");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id)
            .HasConversion(id => id.Value, value => new BuildingId(value))
            .ValueGeneratedNever()
            .HasColumnName("id");

        builder.Property(b => b.CampusId)
            .HasConversion(id => id.Value, value => new CampusId(value))
            .HasColumnName("campus_id")
            .IsRequired();

        builder.Property(b => b.Name).HasColumnName("name").IsRequired();
        builder.Property(b => b.Code).HasColumnName("code");
        builder.Property(b => b.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.Property(b => b.Version)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsRowVersion()
            .ValueGeneratedOnAddOrUpdate();

        builder.HasIndex(b => b.CampusId).HasDatabaseName("ix_buildings_campus_id");
    }
}

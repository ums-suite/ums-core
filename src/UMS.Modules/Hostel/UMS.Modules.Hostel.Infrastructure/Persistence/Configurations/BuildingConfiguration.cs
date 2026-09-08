using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Hostel.Domain.Hostels;

namespace UMS.Modules.Hostel.Infrastructure.Persistence.Configurations;

internal sealed class BuildingConfiguration : IEntityTypeConfiguration<Building>
{
    public void Configure(EntityTypeBuilder<Building> builder)
    {
        builder.ToTable("buildings");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).HasConversion(id => id.Value, value => new BuildingId(value)).ValueGeneratedNever().HasColumnName("id");

        builder.Property(b => b.HostelId).HasConversion(id => id.Value, value => new HostelId(value)).HasColumnName("hostel_id").IsRequired();
        builder.Property(b => b.Name).HasColumnName("name").HasMaxLength(300).IsRequired();
        builder.Property(b => b.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(b => b.Version).HasColumnName("xmin").HasColumnType("xid").IsRowVersion().ValueGeneratedOnAddOrUpdate();

        builder.HasIndex(b => b.HostelId).HasDatabaseName("ix_buildings_hostel_id");
    }
}

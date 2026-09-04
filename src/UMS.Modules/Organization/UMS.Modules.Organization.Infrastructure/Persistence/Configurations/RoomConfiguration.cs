using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Organization.Domain.Facilities;

namespace UMS.Modules.Organization.Infrastructure.Persistence.Configurations;

internal sealed class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder.ToTable("rooms");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id)
            .HasConversion(id => id.Value, value => new RoomId(value))
            .ValueGeneratedNever()
            .HasColumnName("id");

        builder.Property(r => r.BuildingId)
            .HasConversion(id => id.Value, value => new BuildingId(value))
            .HasColumnName("building_id")
            .IsRequired();

        builder.Property(r => r.Name).HasColumnName("name").IsRequired();
        builder.Property(r => r.Capacity).HasColumnName("capacity");
        builder.Property(r => r.RoomType).HasColumnName("room_type");
        builder.Property(r => r.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.Property(r => r.Version)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsRowVersion()
            .ValueGeneratedOnAddOrUpdate();

        builder.HasIndex(r => r.BuildingId).HasDatabaseName("ix_rooms_building_id");
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Hostel.Domain.Hostels;

namespace UMS.Modules.Hostel.Infrastructure.Persistence.Configurations;

internal sealed class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder.ToTable("rooms");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasConversion(id => id.Value, value => new RoomId(value)).ValueGeneratedNever().HasColumnName("id");

        builder.Property(r => r.BuildingId).HasConversion(id => id.Value, value => new BuildingId(value)).HasColumnName("building_id").IsRequired();
        builder.Property(r => r.HostelId).HasConversion(id => id.Value, value => new HostelId(value)).HasColumnName("hostel_id").IsRequired();
        builder.Property(r => r.RoomNumber).HasColumnName("room_number").HasMaxLength(50).IsRequired();
        builder.Property(r => r.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(r => r.Capacity).HasColumnName("capacity").IsRequired();
        builder.Property(r => r.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(r => r.Version).HasColumnName("xmin").HasColumnType("xid").IsRowVersion().ValueGeneratedOnAddOrUpdate();

        builder.HasIndex(r => r.BuildingId).HasDatabaseName("ix_rooms_building_id");
        builder.HasIndex(r => new { r.HostelId, r.Type }).HasDatabaseName("ix_rooms_hostel_id_type");
        builder.HasIndex(r => new { r.BuildingId, r.RoomNumber }).IsUnique().HasDatabaseName("ux_rooms_building_room_number");
    }
}

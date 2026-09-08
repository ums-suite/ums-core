using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Hostel.Domain.Hostels;

namespace UMS.Modules.Hostel.Infrastructure.Persistence.Configurations;

internal sealed class BedConfiguration : IEntityTypeConfiguration<Bed>
{
    public void Configure(EntityTypeBuilder<Bed> builder)
    {
        builder.ToTable("beds");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).HasConversion(id => id.Value, value => new BedId(value)).ValueGeneratedNever().HasColumnName("id");

        builder.Property(b => b.RoomId).HasConversion(id => id.Value, value => new RoomId(value)).HasColumnName("room_id").IsRequired();
        builder.Property(b => b.Label).HasColumnName("label").HasMaxLength(100).IsRequired();
        builder.Property(b => b.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(b => b.Version).HasColumnName("xmin").HasColumnType("xid").IsRowVersion().ValueGeneratedOnAddOrUpdate();

        builder.HasIndex(b => b.RoomId).HasDatabaseName("ix_beds_room_id");
        builder.HasIndex(b => new { b.RoomId, b.Label }).IsUnique().HasDatabaseName("ux_beds_room_label");
    }
}

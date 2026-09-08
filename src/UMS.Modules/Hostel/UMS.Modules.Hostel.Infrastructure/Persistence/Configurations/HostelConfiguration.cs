using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Hostel.Domain.Hostels;

namespace UMS.Modules.Hostel.Infrastructure.Persistence.Configurations;

internal sealed class HostelConfiguration : IEntityTypeConfiguration<Domain.Hostels.Hostel>
{
    public void Configure(EntityTypeBuilder<Domain.Hostels.Hostel> builder)
    {
        builder.ToTable("hostels");
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).HasConversion(id => id.Value, value => new HostelId(value)).ValueGeneratedNever().HasColumnName("id");

        builder.Property(h => h.Name).HasColumnName("name").HasMaxLength(300).IsRequired();
        builder.Property(h => h.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(h => h.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(h => h.Version).HasColumnName("xmin").HasColumnType("xid").IsRowVersion().ValueGeneratedOnAddOrUpdate();
    }
}

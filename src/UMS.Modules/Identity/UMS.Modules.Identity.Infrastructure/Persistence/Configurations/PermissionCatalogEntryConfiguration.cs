using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Identity.Domain.Permissions;

namespace UMS.Modules.Identity.Infrastructure.Persistence.Configurations;

internal sealed class PermissionCatalogEntryConfiguration : IEntityTypeConfiguration<PermissionCatalogEntry>
{
    public void Configure(EntityTypeBuilder<PermissionCatalogEntry> builder)
    {
        builder.ToTable("permissions");
        builder.HasKey(p => p.Key);
        builder.Property(p => p.Key).HasColumnName("key").HasMaxLength(200).ValueGeneratedNever();
        builder.Property(p => p.OwningModule).HasColumnName("owning_module").IsRequired();
        builder.Property(p => p.Description).HasColumnName("description").IsRequired();
        builder.Property(p => p.RegisteredAt).HasColumnName("registered_at").IsRequired();
    }
}

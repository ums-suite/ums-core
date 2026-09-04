using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Identity.Domain.Roles;

namespace UMS.Modules.Identity.Infrastructure.Persistence.Configurations;

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id)
            .HasConversion(id => id.Value, value => new RoleId(value))
            .ValueGeneratedNever()
            .HasColumnName("id");

        builder.Property(r => r.Name).HasColumnName("name").HasColumnType("citext").IsRequired();
        builder.HasIndex(r => r.Name).IsUnique().HasDatabaseName("ux_roles_name");

        builder.Property(r => r.Description).HasColumnName("description");
        builder.Property(r => r.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.Property(r => r.Permissions)
            .HasConversion(permissions => permissions.ToArray(), stored => (IReadOnlyList<string>)stored.ToList())
            .HasColumnName("permissions")
            .IsRequired();

        builder.Property(r => r.RequiresMfa).HasColumnName("requires_mfa").IsRequired().HasDefaultValue(false);
    }
}

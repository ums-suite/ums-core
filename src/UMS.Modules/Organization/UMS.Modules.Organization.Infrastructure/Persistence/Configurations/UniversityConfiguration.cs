using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Organization.Domain.Universities;

namespace UMS.Modules.Organization.Infrastructure.Persistence.Configurations;

internal sealed class UniversityConfiguration : IEntityTypeConfiguration<University>
{
    public void Configure(EntityTypeBuilder<University> builder)
    {
        builder.ToTable("universities");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id)
            .HasConversion(id => id.Value, value => new UniversityId(value))
            .ValueGeneratedNever()
            .HasColumnName("id");

        builder.Property(u => u.Name).HasColumnName("name").IsRequired();
        builder.Property(u => u.Code).HasColumnName("code");
        builder.Property(u => u.Status).HasColumnName("status").HasConversion<string>().IsRequired();
        builder.Property(u => u.CreatedAt).HasColumnName("created_at").IsRequired();

        // design-decisions.md, "Optimistic Concurrency (Version Column) for Hierarchy Edits":
        // backed by Postgres's own `xmin` system column - idiomatic EF Core + Npgsql concurrency
        // support, no application-managed version-increment logic needed.
        builder.Property(u => u.Version)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsRowVersion()
            .ValueGeneratedOnAddOrUpdate();
    }
}

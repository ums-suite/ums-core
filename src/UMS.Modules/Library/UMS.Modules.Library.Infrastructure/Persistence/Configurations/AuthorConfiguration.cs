using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Library.Domain.Catalog;

namespace UMS.Modules.Library.Infrastructure.Persistence.Configurations;

internal sealed class AuthorConfiguration : IEntityTypeConfiguration<Author>
{
    public void Configure(EntityTypeBuilder<Author> builder)
    {
        builder.ToTable("authors");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasConversion(id => id.Value, value => new AuthorId(value)).ValueGeneratedNever().HasColumnName("id");

        builder.Property(a => a.Name).HasColumnName("name").HasMaxLength(300).IsRequired();
        builder.Property(a => a.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(a => a.Name).HasDatabaseName("ix_authors_name");
    }
}

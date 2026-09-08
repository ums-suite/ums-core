using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Library.Domain.Catalog;

namespace UMS.Modules.Library.Infrastructure.Persistence.Configurations;

internal sealed class BookConfiguration : IEntityTypeConfiguration<Book>
{
    public void Configure(EntityTypeBuilder<Book> builder)
    {
        builder.ToTable("books");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).HasConversion(id => id.Value, value => new BookId(value)).ValueGeneratedNever().HasColumnName("id");

        builder.Property(b => b.Title).HasColumnName("title").HasMaxLength(500).IsRequired();
        builder.Property(b => b.Isbn).HasColumnName("isbn").HasMaxLength(20);
        builder.Property(b => b.CategoryId)
            .HasConversion(id => id == null ? (Guid?)null : id.Value.Value, value => value == null ? (CategoryId?)null : new CategoryId(value.Value))
            .HasColumnName("category_id");
        builder.Property(b => b.Edition).HasColumnName("edition").HasMaxLength(100);
        builder.Property(b => b.IsOpenAccessDigital).HasColumnName("is_open_access_digital").IsRequired();
        builder.Property(b => b.Withdrawn).HasColumnName("withdrawn").IsRequired();
        builder.Property(b => b.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(b => b.WithdrawnAt).HasColumnName("withdrawn_at");
        builder.Property(b => b.Version).HasColumnName("xmin").HasColumnType("xid").IsRowVersion().ValueGeneratedOnAddOrUpdate();

        builder.Property(b => b.AuthorIds)
            .HasConversion(JsonListConverters.GuidList)
            .Metadata.SetValueComparer(JsonListConverters.GuidListComparer);
        builder.Property(b => b.AuthorIds).HasColumnName("author_ids").HasColumnType("jsonb").IsRequired();

        builder.HasIndex(b => b.Title).HasDatabaseName("ix_books_title");
        builder.HasIndex(b => b.Isbn).HasDatabaseName("ix_books_isbn");
        builder.HasIndex(b => b.CategoryId).HasDatabaseName("ix_books_category_id");
    }
}

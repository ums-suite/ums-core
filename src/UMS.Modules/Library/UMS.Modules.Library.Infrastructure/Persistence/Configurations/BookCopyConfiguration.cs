using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Library.Domain.Catalog;

namespace UMS.Modules.Library.Infrastructure.Persistence.Configurations;

internal sealed class BookCopyConfiguration : IEntityTypeConfiguration<BookCopy>
{
    public void Configure(EntityTypeBuilder<BookCopy> builder)
    {
        builder.ToTable("book_copies");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasConversion(id => id.Value, value => new BookCopyId(value)).ValueGeneratedNever().HasColumnName("id");

        builder.Property(c => c.BookId).HasConversion(id => id.Value, value => new BookId(value)).HasColumnName("book_id").IsRequired();
        builder.Property(c => c.AccessionNumber).HasColumnName("accession_number").HasMaxLength(100).IsRequired();
        builder.Property(c => c.Condition).HasColumnName("condition").HasMaxLength(50).IsRequired();
        builder.Property(c => c.CopyType).HasColumnName("copy_type").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(c => c.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(c => c.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(c => c.LostAt).HasColumnName("lost_at");
        builder.Property(c => c.WithdrawnAt).HasColumnName("withdrawn_at");
        builder.Property(c => c.Version).HasColumnName("xmin").HasColumnType("xid").IsRowVersion().ValueGeneratedOnAddOrUpdate();

        builder.HasIndex(c => c.BookId).HasDatabaseName("ix_book_copies_book_id");
        builder.HasIndex(c => new { c.BookId, c.Status }).HasDatabaseName("ix_book_copies_book_id_status");
        builder.HasIndex(c => c.AccessionNumber).IsUnique().HasDatabaseName("ux_book_copies_accession_number");
    }
}

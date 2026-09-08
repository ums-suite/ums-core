using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Library.Domain.Reservations;

namespace UMS.Modules.Library.Infrastructure.Persistence.Configurations;

internal sealed class BookReservationSupplyFlagConfiguration : IEntityTypeConfiguration<BookReservationSupplyFlag>
{
    public void Configure(EntityTypeBuilder<BookReservationSupplyFlag> builder)
    {
        builder.ToTable("book_reservation_supply_flags");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).ValueGeneratedNever().HasColumnName("id");

        builder.Property(f => f.BookId).HasColumnName("book_id").IsRequired();
        builder.Property(f => f.RemainingCopyCount).HasColumnName("remaining_copy_count").IsRequired();
        builder.Property(f => f.QueueDepth).HasColumnName("queue_depth").IsRequired();
        builder.Property(f => f.Reason).HasColumnName("reason").HasMaxLength(1000).IsRequired();
        builder.Property(f => f.SourceEventReference).HasColumnName("source_event_reference").HasMaxLength(200).IsRequired();
        builder.Property(f => f.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(f => f.BookId).HasDatabaseName("ix_book_reservation_supply_flags_book_id");
    }
}

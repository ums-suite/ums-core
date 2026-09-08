using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Library.Domain.Reservations;

namespace UMS.Modules.Library.Infrastructure.Persistence.Configurations;

/// <summary>
/// requirement-spec.md §4 invariant, extended here (not itself spec-mandated, but a reasonable
/// defensive addition): a borrower may hold at most one OPEN (non-terminal) Reservation per Book.
/// <see cref="ReservationStatus"/>'s own remarks list <c>Queued</c> and <c>Offered</c> as the only
/// two non-terminal statuses - <c>Claimed</c>/<c>Expired</c> are both terminal - so those two,
/// exactly, form the correct filter (checked against the enum directly, not assumed).
/// </summary>
internal sealed class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
{
    private const string OpenFilter = "status IN ('Queued', 'Offered')";

    public void Configure(EntityTypeBuilder<Reservation> builder)
    {
        builder.ToTable("reservations");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasConversion(id => id.Value, value => new ReservationId(value)).ValueGeneratedNever().HasColumnName("id");

        builder.Property(r => r.BookId).HasColumnName("book_id").IsRequired();
        builder.Property(r => r.BorrowerId).HasColumnName("borrower_id").IsRequired();
        builder.Property(r => r.BorrowerType).HasColumnName("borrower_type").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(r => r.Priority).HasColumnName("priority").IsRequired();
        builder.Property(r => r.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(r => r.OfferedCopyId).HasColumnName("offered_copy_id");
        builder.Property(r => r.OfferedAt).HasColumnName("offered_at");
        builder.Property(r => r.ClaimWindowExpiresAt).HasColumnName("claim_window_expires_at");
        builder.Property(r => r.ClaimedAt).HasColumnName("claimed_at");
        builder.Property(r => r.ExpiredAt).HasColumnName("expired_at");
        builder.Property(r => r.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(r => r.Version).HasColumnName("xmin").HasColumnType("xid").IsRowVersion().ValueGeneratedOnAddOrUpdate();

        // design-decisions.md "Reservation-Queue Fairness and Claim Mechanism" - the fulfillment SQL's
        // own ORDER BY selects on exactly these two columns.
        builder.HasIndex(r => new { r.BookId, r.Status, r.Priority, r.CreatedAt }).HasDatabaseName("ix_reservations_book_status_priority_created");
        builder.HasIndex(r => r.OfferedCopyId).HasDatabaseName("ix_reservations_offered_copy_id");
        builder.HasIndex(r => r.BorrowerId).HasDatabaseName("ix_reservations_borrower_id");

        builder.HasIndex(r => new { r.BookId, r.BorrowerId }).IsUnique().HasDatabaseName("ux_reservations_book_borrower_open").HasFilter(OpenFilter);
    }
}

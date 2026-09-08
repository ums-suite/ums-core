using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Library.Domain.Loans;

namespace UMS.Modules.Library.Infrastructure.Persistence.Configurations;

/// <summary>
/// design-decisions.md "Copy-Issuance Concurrency Control Pattern": the partial-unique-index backstop
/// behind <c>LoanService</c>'s own pessimistic BookCopy-row lock.
///
/// <para>
/// <b>Filtered on <c>status = 'Active'</c> alone - NOT the multi-status non-terminal list Hostel's
/// own <c>AllocationConfiguration</c> uses.</b> Hostel's <c>Allocation</c> passes through an earlier
/// non-terminal stage (<c>Pending</c>) before reaching <c>Active</c>, so its own oversell race
/// produces two competing <c>Pending</c> rows a literal <c>= 'Active'</c> filter would never catch -
/// that correction does not apply here. <see cref="LoanStatus"/>'s own remarks confirm
/// <see cref="Loan.Issue"/> transitions a Loan directly to <see cref="LoanStatus.Active"/> with no
/// earlier non-terminal stage at all, so <c>status = 'Active'</c> alone already covers every
/// concurrently-issued Loan for the same BookCopy - this was checked against Library's own
/// LoanStatus enum before writing this filter, per the mechanism note this exact class of bug
/// (Hostel's own first-pass mistake) warns about, not copied blindly.
/// </para>
/// </summary>
internal sealed class LoanConfiguration : IEntityTypeConfiguration<Loan>
{
    private const string ActiveFilter = "status = 'Active'";

    public void Configure(EntityTypeBuilder<Loan> builder)
    {
        builder.ToTable("loans");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasConversion(id => id.Value, value => new LoanId(value)).ValueGeneratedNever().HasColumnName("id");

        builder.Property(l => l.BookCopyId).HasColumnName("book_copy_id").IsRequired();
        builder.Property(l => l.BookId).HasColumnName("book_id").IsRequired();
        builder.Property(l => l.BorrowerId).HasColumnName("borrower_id").IsRequired();
        builder.Property(l => l.BorrowerType).HasColumnName("borrower_type").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(l => l.IssuedByUserId).HasColumnName("issued_by_user_id");
        builder.Property(l => l.IssuedAt).HasColumnName("issued_at").IsRequired();
        builder.Property(l => l.DueDate).HasColumnName("due_date").IsRequired();
        builder.Property(l => l.RenewalCount).HasColumnName("renewal_count").IsRequired();
        builder.Property(l => l.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(l => l.ReturnedAt).HasColumnName("returned_at");
        builder.Property(l => l.LostWriteOffAt).HasColumnName("lost_write_off_at");
        builder.Property(l => l.Version).HasColumnName("xmin").HasColumnType("xid").IsRowVersion().ValueGeneratedOnAddOrUpdate();

        builder.HasIndex(l => l.BorrowerId).HasDatabaseName("ix_loans_borrower_id");
        builder.HasIndex(l => new { l.Status, l.DueDate }).HasDatabaseName("ix_loans_status_due_date");

        // requirement-spec.md §4 invariant "A BookCopy has at most one active Loan at a time" - see
        // this class's own remarks for why 'Active' alone (not a multi-status list) is correct here.
        builder.HasIndex(l => l.BookCopyId).IsUnique().HasDatabaseName("ux_loans_bookcopy_active").HasFilter(ActiveFilter);
    }
}

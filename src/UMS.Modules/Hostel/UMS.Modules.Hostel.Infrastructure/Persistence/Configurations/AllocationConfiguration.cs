using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Hostel.Domain.Allocations;

namespace UMS.Modules.Hostel.Infrastructure.Persistence.Configurations;

/// <summary>
/// design-decisions.md "Bed-Allocation Concurrency Control Pattern": the partial-unique-index
/// backstop behind <c>AllocationService</c>'s own pessimistic Bed-row lock.
///
/// <para>
/// <b>Non-terminal, not literally "= Active" - read before changing.</b> requirement-spec.md §4's
/// prose names the filter as <c>WHERE status = 'Active'</c>, but the oversell race edge-cases.md
/// actually describes (two officers concurrently approving the same Bed) produces two competing
/// <see cref="AllocationStatus.Pending"/> rows, not two <see cref="AllocationStatus.Active"/> ones -
/// a literal <c>= 'Active'</c> filter would never fire for the exact race the backstop exists to
/// catch, since neither racing writer reaches <c>Active</c> until check-in, long after the race
/// resolves. Filtering on every NON-TERMINAL status (<c>Pending</c>, <c>FeePaid</c>, <c>Active</c>)
/// instead - mirroring Finance's own <c>Payment</c> backstop
/// (<c>ux_payments_invoice_nonterminal</c>, <c>status IN ('Initiated', 'Pending')</c>) - is what
/// actually delivers the guarantee both requirement-spec §4 and edge-cases.md state: "the losing
/// transaction fails on the DB constraint." This is a deliberate correction of the spec's simplified
/// prose to match its own stated intent, not a deviation from it.
/// </para>
/// </summary>
internal sealed class AllocationConfiguration : IEntityTypeConfiguration<Allocation>
{
    private const string NonTerminalFilter = "status IN ('Pending', 'FeePaid', 'Active')";

    public void Configure(EntityTypeBuilder<Allocation> builder)
    {
        builder.ToTable("allocations");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasConversion(id => id.Value, value => new AllocationId(value)).ValueGeneratedNever().HasColumnName("id");

        builder.Property(a => a.StudentId).HasColumnName("student_id").IsRequired();
        builder.Property(a => a.BedId).HasColumnName("bed_id").IsRequired();
        builder.Property(a => a.RoomId).HasColumnName("room_id").IsRequired();
        builder.Property(a => a.HostelId).HasColumnName("hostel_id").IsRequired();
        builder.Property(a => a.HostelApplicationId).HasColumnName("hostel_application_id").IsRequired();
        builder.Property(a => a.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(a => a.InvoiceId).HasColumnName("invoice_id");
        builder.Property(a => a.FeeGraceDeadline).HasColumnName("fee_grace_deadline").IsRequired();
        builder.Property(a => a.CheckOutKind).HasColumnName("check_out_kind").HasConversion<string>().HasMaxLength(30);
        builder.Property(a => a.RefundRequested).HasColumnName("refund_requested").IsRequired();
        builder.Property(a => a.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(a => a.FeePaidAt).HasColumnName("fee_paid_at");
        builder.Property(a => a.ActivatedAt).HasColumnName("activated_at");
        builder.Property(a => a.CheckedOutAt).HasColumnName("checked_out_at");
        builder.Property(a => a.ExpiredAt).HasColumnName("expired_at");
        builder.Property(a => a.Version).HasColumnName("xmin").HasColumnType("xid").IsRowVersion().ValueGeneratedOnAddOrUpdate();

        builder.HasIndex(a => a.StudentId).HasDatabaseName("ix_allocations_student_id");
        builder.HasIndex(a => a.InvoiceId).HasDatabaseName("ix_allocations_invoice_id");
        builder.HasIndex(a => new { a.Status, a.FeeGraceDeadline }).HasDatabaseName("ix_allocations_status_grace_deadline");

        // requirement-spec.md §4 invariant "One active Student per Bed" / "Allocation creation is
        // concurrency-safe" - see this class's own remarks for the non-terminal filter choice.
        builder.HasIndex(a => a.BedId).IsUnique().HasDatabaseName("ux_allocations_bed_nonterminal").HasFilter(NonTerminalFilter);

        // requirement-spec.md §4 invariant "One active Allocation per Student".
        builder.HasIndex(a => a.StudentId).IsUnique().HasDatabaseName("ux_allocations_student_nonterminal").HasFilter(NonTerminalFilter);
    }
}

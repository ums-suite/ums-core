using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Library.Domain.Fines;

namespace UMS.Modules.Library.Infrastructure.Persistence.Configurations;

/// <summary>
/// <see cref="Fine.Amount"/> is a TOP-LEVEL aggregate's own Money field (Fine is its own root, never
/// nested inside another aggregate's owned collection) - <c>ComplexProperty</c> is safe to use
/// directly here, mirroring Finance's own <c>PaymentConfiguration</c> exactly. This is a DIFFERENT
/// shape from an owned-collection child needing a Money field (this module has no such child - see
/// <see cref="Domain.Fines.FineAccrual"/>'s own remarks on why it stores a plain scalar instead).
/// </summary>
internal sealed class FineConfiguration : IEntityTypeConfiguration<Fine>
{
    public void Configure(EntityTypeBuilder<Fine> builder)
    {
        builder.ToTable("fines");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).HasConversion(id => id.Value, value => new FineId(value)).ValueGeneratedNever().HasColumnName("id");

        builder.Property(f => f.LoanId).HasColumnName("loan_id").IsRequired();
        builder.Property(f => f.BorrowerId).HasColumnName("borrower_id").IsRequired();
        builder.Property(f => f.BorrowerType).HasColumnName("borrower_type").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(f => f.Reason).HasColumnName("reason").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(f => f.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(f => f.InvoiceId).HasColumnName("invoice_id");
        builder.Property(f => f.WaivedByUserId).HasColumnName("waived_by_user_id");
        builder.Property(f => f.WaivedReason).HasColumnName("waived_reason").HasMaxLength(1000);
        builder.Property(f => f.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(f => f.SettledAt).HasColumnName("settled_at");
        builder.Property(f => f.WaivedAt).HasColumnName("waived_at");
        builder.Property(f => f.Version).HasColumnName("xmin").HasColumnType("xid").IsRowVersion().ValueGeneratedOnAddOrUpdate();

        builder.ComplexProperty(f => f.Amount, amount =>
        {
            amount.Property(a => a.Amount).HasColumnName("amount").HasPrecision(12, 2).IsRequired();
            amount.Property(a => a.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
        });

        builder.HasIndex(f => f.LoanId).HasDatabaseName("ix_fines_loan_id");
        builder.HasIndex(f => f.BorrowerId).HasDatabaseName("ix_fines_borrower_id");
        builder.HasIndex(f => f.InvoiceId).HasDatabaseName("ix_fines_invoice_id");
        builder.HasIndex(f => new { f.BorrowerId, f.Status }).HasDatabaseName("ix_fines_borrower_id_status");
    }
}

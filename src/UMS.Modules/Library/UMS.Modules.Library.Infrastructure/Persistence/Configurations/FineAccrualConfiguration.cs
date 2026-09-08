using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Library.Domain.Fines;

namespace UMS.Modules.Library.Infrastructure.Persistence.Configurations;

/// <summary>See <see cref="FineAccrual"/>'s own remarks - a standalone top-level entity, not an <c>OwnsMany</c> child of <see cref="Fine"/>, deliberately sidestepping EF Core 10.0.11's owned-collection insert-vs-update misfire rather than working around it with a shadow Ordinal key.</summary>
internal sealed class FineAccrualConfiguration : IEntityTypeConfiguration<FineAccrual>
{
    public void Configure(EntityTypeBuilder<FineAccrual> builder)
    {
        builder.ToTable("fine_accruals");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever().HasColumnName("id");

        builder.Property(a => a.FineId).HasColumnName("fine_id").IsRequired();
        builder.Property(a => a.LoanId).HasColumnName("loan_id").IsRequired();
        builder.Property(a => a.AccrualDate).HasColumnName("accrual_date").IsRequired();
        builder.Property(a => a.Amount).HasColumnName("amount").HasPrecision(12, 2).IsRequired();
        builder.Property(a => a.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(a => a.FineId).HasDatabaseName("ix_fine_accruals_fine_id");

        // design-decisions.md "Fine-Accrual Job Idempotency" - the unique-constraint backstop behind
        // the accrual job's own row-lock-and-recheck.
        builder.HasIndex(a => new { a.LoanId, a.AccrualDate }).IsUnique().HasDatabaseName("ux_fine_accruals_loan_date");
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Finance.Domain.Payments;
using UMS.Modules.Finance.Domain.Reconciliation;

namespace UMS.Modules.Finance.Infrastructure.Persistence.Configurations;

/// <summary>FIN-14: an own table, own entity, append-only - mirrors <c>LedgerEntryConfiguration</c>'s own posture exactly (no Update/Delete anywhere on <see cref="Application.Abstractions.IReconciliationExceptionRepository"/>).</summary>
internal sealed class ReconciliationExceptionConfiguration : IEntityTypeConfiguration<ReconciliationException>
{
    public void Configure(EntityTypeBuilder<ReconciliationException> builder)
    {
        builder.ToTable("reconciliation_exceptions");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasConversion(id => id.Value, value => new ReconciliationExceptionId(value)).ValueGeneratedNever().HasColumnName("id");

        builder.Property(r => r.PaymentId).HasConversion(id => id.Value, value => new PaymentId(value)).HasColumnName("payment_id").IsRequired();
        builder.Property(r => r.PaymentTransactionId).HasConversion(id => id.Value, value => new PaymentTransactionId(value)).HasColumnName("payment_transaction_id").IsRequired();
        builder.Property(r => r.SettlementDate).HasColumnName("settlement_date").IsRequired();
        builder.Property(r => r.Reason).HasColumnName("reason").HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(r => r.ExpectedGatewayTransactionId).HasColumnName("expected_gateway_transaction_id").HasMaxLength(200);
        builder.Property(r => r.ActualGatewayTransactionId).HasColumnName("actual_gateway_transaction_id").HasMaxLength(200);
        builder.Property(r => r.ActualGatewayStatus).HasColumnName("actual_gateway_status").HasMaxLength(32);
        builder.Property(r => r.Details).HasColumnName("details").HasMaxLength(2000).IsRequired();
        builder.Property(r => r.DetectedAt).HasColumnName("detected_at").IsRequired();

        builder.HasIndex(r => r.PaymentId).HasDatabaseName("ix_reconciliation_exceptions_payment_id");
        builder.HasIndex(r => r.SettlementDate).HasDatabaseName("ix_reconciliation_exceptions_settlement_date");
    }
}

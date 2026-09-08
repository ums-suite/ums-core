using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Finance.Domain.Invoices;
using UMS.Modules.Finance.Domain.Payments;

namespace UMS.Modules.Finance.Infrastructure.Persistence.Configurations;

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasConversion(id => id.Value, value => new PaymentId(value)).ValueGeneratedNever().HasColumnName("id");

        builder.Property(p => p.InvoiceId).HasConversion(id => id.Value, value => new InvoiceId(value)).HasColumnName("invoice_id").IsRequired();
        builder.Property(p => p.OwnerId).HasColumnName("owner_id").IsRequired();
        builder.Property(p => p.SourceModule).HasColumnName("source_module").HasMaxLength(100).IsRequired();
        builder.Property(p => p.SourceReferenceId).HasColumnName("source_reference_id").HasMaxLength(200).IsRequired();
        builder.Property(p => p.InitiatedByUserId).HasColumnName("initiated_by_user_id").IsRequired();
        builder.Property(p => p.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(200).IsRequired();
        builder.Property(p => p.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(p => p.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(p => p.Version).HasColumnName("xmin").HasColumnType("xid").IsRowVersion().ValueGeneratedOnAddOrUpdate();

        builder.ComplexProperty(p => p.Amount, amount =>
        {
            amount.Property(a => a.Amount).HasColumnName("amount").HasPrecision(12, 2).IsRequired();
            amount.Property(a => a.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
        });

        // requirement-spec.md §4: the IdempotencyKey uniqueness constraint - "checked before the
        // gateway call, permanently unique (no TTL/replay-window expiry)" (design-decisions.md).
        builder.HasIndex(p => p.IdempotencyKey).IsUnique().HasDatabaseName("ux_payments_idempotency_key");

        // design-decisions.md "Invoice-Level Concurrency Control for Payment Initiation": the
        // partial-unique-index backstop behind PaymentService's own pessimistic Invoice row lock -
        // "at most one non-terminal Payment per Invoice". Residual note (edge-cases.md): this
        // predicate must stay in lockstep with any future addition to PaymentStatus.
        builder.HasIndex(p => p.InvoiceId)
            .IsUnique()
            .HasDatabaseName("ux_payments_invoice_nonterminal")
            .HasFilter("status IN ('Initiated', 'Pending')");

        builder.HasIndex(p => p.OwnerId).HasDatabaseName("ix_payments_owner_id");

        // FIN-9/FIN-10's stuck-payment sweep candidate scan.
        builder.HasIndex(p => new { p.Status, p.UpdatedAt }).HasDatabaseName("ix_payments_status_updated_at");

        builder.OwnsMany(p => p.Transactions, transaction =>
        {
            transaction.ToTable("payment_transactions");
            transaction.Property(t => t.Id).HasConversion(id => id.Value, value => new PaymentTransactionId(value)).ValueGeneratedNever().HasColumnName("id");
            transaction.WithOwner().HasForeignKey(t => t.PaymentId);
            transaction.Property(t => t.PaymentId).HasConversion(id => id.Value, value => new PaymentId(value)).HasColumnName("payment_id");
            transaction.HasKey(t => t.Id);

            transaction.Property(t => t.GatewayName).HasColumnName("gateway_name").HasMaxLength(100).IsRequired();
            transaction.Property(t => t.AttemptNumber).HasColumnName("attempt_number").IsRequired();
            transaction.Property(t => t.GatewaySessionReference).HasColumnName("gateway_session_reference").HasMaxLength(200);
            transaction.Property(t => t.GatewayTransactionId).HasColumnName("gateway_transaction_id").HasMaxLength(200);
            transaction.Property(t => t.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32).IsRequired();
            transaction.Property(t => t.CreatedAt).HasColumnName("created_at").IsRequired();
            transaction.Property(t => t.UpdatedAt).HasColumnName("updated_at").IsRequired();
            transaction.Property(t => t.FailureReason).HasColumnName("failure_reason").HasMaxLength(2000);
            transaction.Ignore(t => t.IsNonTerminal);

            // requirement-spec.md §4: "PaymentTransaction.gatewayTransactionId carries its own
            // DB-level uniqueness constraint - a duplicate webhook or a replayed gateway callback
            // can never post twice." Partial (nullable until the first webhook reports it).
            transaction.HasIndex(t => t.GatewayTransactionId)
                .IsUnique()
                .HasDatabaseName("ux_payment_transactions_gateway_transaction_id")
                .HasFilter("gateway_transaction_id IS NOT NULL");
        });
        builder.Navigation(p => p.Transactions).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(p => p.Refunds, ConfigureRefund);
        builder.Navigation(p => p.Refunds).UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    // FIN-11: a separate, explicitly-typed method - not an inline lambda - mirroring
    // InvoiceConfiguration.ConfigureItem's own remarks (an inline lambda whose own body also calls a
    // second overloaded generic builder method defeats OwnsMany's TRelatedEntity inference here).
    //
    // Refund.Id (a real business Guid, Refund's own external identity) is deliberately NOT this
    // owned collection's EF key - see Refund's own remarks: EF Core 10.0.11 misidentifies a brand-new
    // child row as an UPDATE when its key is a fully caller-supplied value and the parent Payment is
    // already tracked/persisted (exactly Refund's own shape - added to an EXISTING Payment, unlike
    // PaymentTransaction which is always added to a still-Added, not-yet-persisted Payment). The key
    // here is instead a shadow Ordinal position column, the identical fix InvoiceItem's own mapping
    // above already applies for the same underlying limitation.
    private static void ConfigureRefund(OwnedNavigationBuilder<Payment, Refund> refund)
    {
        refund.ToTable("refunds");
        refund.WithOwner().HasForeignKey("PaymentId");
        refund.Property<PaymentId>("PaymentId").HasConversion(id => id.Value, value => new PaymentId(value)).HasColumnName("payment_id");
        refund.Property<int>("Ordinal").HasColumnName("ordinal");
        refund.HasKey("PaymentId", "Ordinal");

        refund.Property(r => r.Id).HasConversion(id => id.Value, value => new RefundId(value)).HasColumnName("id").IsRequired();
        refund.Property(r => r.PaymentTransactionId).HasConversion(id => id.Value, value => new PaymentTransactionId(value)).HasColumnName("payment_transaction_id").IsRequired();
        refund.Property(r => r.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32).IsRequired();
        refund.Property(r => r.Method).HasColumnName("method").HasConversion<string>().HasMaxLength(32).IsRequired();
        refund.Property(r => r.RequestedByUserId).HasColumnName("requested_by_user_id").IsRequired();
        refund.Property(r => r.GatewayRefundReference).HasColumnName("gateway_refund_reference").HasMaxLength(200);
        refund.Property(r => r.FailureReason).HasColumnName("failure_reason").HasMaxLength(2000);
        refund.Property(r => r.CreatedAt).HasColumnName("created_at").IsRequired();
        refund.Property(r => r.UpdatedAt).HasColumnName("updated_at").IsRequired();

        // Plain scalars, not a nested Money mapping - the identical ComplexProperty-one-level-inside-
        // an-owned-collection limitation InvoiceItem's own mapping documents (a struct Money also
        // rules out OwnsOne/OwnsMany here, same reasoning).
        refund.Property(r => r.Amount).HasColumnName("amount").HasPrecision(12, 2).IsRequired();
        refund.Property(r => r.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
        refund.Ignore(r => r.AsMoney);

        // Refund.Id is a real business identity (external references - audit, notifications, the API
        // response) even though it is not the EF key - unique so it is still safely addressable.
        refund.HasIndex(r => r.Id).IsUnique().HasDatabaseName("ux_refunds_id");
    }
}

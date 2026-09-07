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
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Finance.Domain.Ledger;

namespace UMS.Modules.Finance.Infrastructure.Persistence.Configurations;

/// <summary>requirement-spec.md §4: append-only - no <c>UPDATE</c>/<c>DELETE</c> at the API layer or the database GRANT level (mirrors ADR-0012's Audit discipline). The GRANT-level lockdown is applied by a raw-SQL migration step alongside this table's creation, the same mechanism Audit's own <c>AuditLogEntry</c> uses.</summary>
internal sealed class LedgerEntryConfiguration : IEntityTypeConfiguration<LedgerEntry>
{
    public void Configure(EntityTypeBuilder<LedgerEntry> builder)
    {
        builder.ToTable("ledger_entries");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasConversion(id => id.Value, value => new LedgerEntryId(value)).ValueGeneratedNever().HasColumnName("id");

        builder.Property(l => l.EntryType).HasColumnName("entry_type").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(l => l.ReferenceType).HasColumnName("reference_type").HasMaxLength(100).IsRequired();
        builder.Property(l => l.ReferenceId).HasColumnName("reference_id").IsRequired();
        builder.Property(l => l.Amount).HasColumnName("amount").HasPrecision(12, 2).IsRequired();
        builder.Property(l => l.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
        builder.Property(l => l.Description).HasColumnName("description").HasMaxLength(1000).IsRequired();
        builder.Property(l => l.CorrelationId).HasColumnName("correlation_id").HasMaxLength(100).IsRequired();
        builder.Property(l => l.OccurredAt).HasColumnName("occurred_at").IsRequired();

        builder.HasIndex(l => new { l.ReferenceType, l.ReferenceId }).HasDatabaseName("ix_ledger_entries_reference");
        builder.HasIndex(l => l.OccurredAt).HasDatabaseName("ix_ledger_entries_occurred_at");
    }
}

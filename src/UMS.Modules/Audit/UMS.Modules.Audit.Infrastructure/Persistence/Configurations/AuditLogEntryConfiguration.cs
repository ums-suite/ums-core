using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMS.Modules.Audit.Domain.Entries;

namespace UMS.Modules.Audit.Infrastructure.Persistence.Configurations;

/// <summary>
/// AUD-1/AUD-3: the append-only ledger's mapping. The physical table is created via raw SQL in
/// the initial migration (<c>PARTITION BY RANGE (occurred_at)</c> - EF's fluent
/// <c>CreateTable</c>/migration-diffing has no concept of Postgres declarative partitioning), but
/// this configuration is still what every query against it (and every future migration diff)
/// compiles against, so it must describe the physical shape exactly.
///
/// <para>
/// The primary key is the composite <c>(occurred_at, id)</c>, not <c>id</c> alone - Postgres
/// requires every unique constraint/primary key on a partitioned table to include the partition
/// key column. A plain, non-unique index on <c>id</c> alone (added in the migration) keeps
/// single-id lookups (<c>GET /audit/entries/{id}</c>) fast without needing <c>occurred_at</c> in
/// the query; true uniqueness of the ULID <c>id</c> is guaranteed by construction (128 bits of
/// timestamp+randomness), not by a database constraint, which is the accepted trade-off of using
/// a partitioned append-only table this way.
/// </para>
/// </summary>
internal sealed class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> builder)
    {
        builder.ToTable("audit_log_entries");
        builder.HasKey(e => new { e.OccurredAt, e.Id });

        builder.Property(e => e.Id)
            .HasConversion(id => id.Value, value => new AuditLogEntryId(value))
            .HasColumnName("id")
            .HasColumnType("varchar(26)")
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(e => e.OccurredAt).HasColumnName("occurred_at").IsRequired();

        builder.Property(e => e.ActorId).HasColumnName("actor_id").IsRequired();
        builder.Property(e => e.ActorType).HasColumnName("actor_type").HasConversion<string>().IsRequired();
        builder.Property(e => e.IpAddress).HasColumnName("ip_address");
        builder.Property(e => e.Application).HasColumnName("application").IsRequired();
        builder.Property(e => e.EntityType).HasColumnName("entity_type").IsRequired();
        builder.Property(e => e.EntityId).HasColumnName("entity_id").IsRequired();

        builder.Property(e => e.Action)
            .HasConversion(action => action.Value, value => AuditAction.Create(value).Value)
            .HasColumnName("action")
            .IsRequired();

        builder.Property(e => e.BeforeValueJson).HasColumnName("before_value_json").HasColumnType("jsonb");
        builder.Property(e => e.AfterValueJson).HasColumnName("after_value_json").HasColumnType("jsonb");
        builder.Property(e => e.CorrelationId).HasColumnName("correlation_id").IsRequired();
        builder.Property(e => e.Reason).HasColumnName("reason");
        builder.Property(e => e.OrganizationScopeId).HasColumnName("organization_scope_id");

        // Non-unique - see this type's own remarks above for why id alone cannot be a unique
        // index on a table partitioned by a different column.
        builder.HasIndex(e => e.Id).HasDatabaseName("ix_audit_log_entries_id");

        // AUD-8's entity-history query shape: WHERE entity_type = ... AND entity_id = ...
        // ORDER BY id.
        builder.HasIndex(e => new { e.EntityType, e.EntityId, e.Id }).HasDatabaseName("ix_audit_log_entries_entity");

        builder.HasIndex(e => new { e.ActorId, e.OccurredAt }).HasDatabaseName("ix_audit_log_entries_actor");
        builder.HasIndex(e => e.CorrelationId).HasDatabaseName("ix_audit_log_entries_correlation_id");
    }
}
